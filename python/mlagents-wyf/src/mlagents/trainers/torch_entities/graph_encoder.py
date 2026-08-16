"""Relational graph encoders for construction-task scheduling.

Author: wyf
"""

from collections import deque
import csv
from functools import lru_cache
from pathlib import Path
from typing import List, Set, Tuple

from mlagents.torch_utils import nn, torch

TASK_FEATURE_DIMENSION = 15
AGENT_FEATURE_DIMENSION = 7
RELATION_COUNT = 3
SUPPORTED_TASK_COUNTS = (86, 172, 226)


def _parse_ids(value: str) -> List[int]:
    if not value or not value.strip():
        return []
    return [int(item.strip()) for item in value.split("+") if item.strip()]


def _compute_reachability(adjacency: List[Set[int]]) -> List[Set[int]]:
    reachable: List[Set[int]] = [set() for _ in adjacency]
    for start_index, neighbors in enumerate(adjacency):
        queue = deque(neighbors)
        visited = set(neighbors)
        while queue:
            current_index = queue.popleft()
            reachable[start_index].add(current_index)
            for next_index in adjacency[current_index]:
                if next_index not in visited:
                    visited.add(next_index)
                    queue.append(next_index)
    return reachable


@lru_cache(maxsize=len(SUPPORTED_TASK_COUNTS))
def load_graph_topology(task_count: int) -> Tuple[torch.Tensor, torch.Tensor]:
    """Load and validate the fixed topology for one supported problem size."""
    if task_count not in SUPPORTED_TASK_COUNTS:
        raise ValueError(
            f"Unsupported task count {task_count}; expected one of {SUPPORTED_TASK_COUNTS}."
        )
    csv_path = (
        Path(__file__).resolve().parent
        / "graph_data"
        / f"{task_count}_tasks.CSV"
    )
    if not csv_path.is_file():
        raise FileNotFoundError(f"Graph topology data was not found: {csv_path}")
    with csv_path.open("r", encoding="utf-8-sig", newline="") as csv_file:
        rows = [row for row in csv.reader(csv_file) if any(cell.strip() for cell in row)]
    if len(rows) != task_count:
        raise ValueError(
            f"{csv_path.name} contains {len(rows)} non-empty rows; expected {task_count}."
        )
    if any(len(row) <= 10 for row in rows):
        raise ValueError(f"{csv_path.name} must contain at least 11 columns.")
    task_ids = [int(row[0]) for row in rows]
    if len(task_ids) != len(set(task_ids)):
        raise ValueError(f"{csv_path.name} contains duplicate task IDs.")
    id_to_index = {task_id: index for index, task_id in enumerate(task_ids)}
    precedence = [set() for _ in rows]
    for row in rows:
        current_id = int(row[0])
        current_index = id_to_index[current_id]
        for predecessor_id in _parse_ids(row[3]):
            if predecessor_id not in id_to_index:
                raise ValueError(
                    f"Task {current_id} references missing predecessor {predecessor_id}."
                )
            precedence[id_to_index[predecessor_id]].add(current_index)
    reachable = _compute_reachability(precedence)
    cyclic_task_ids = [
        task_ids[index] for index in range(task_count) if index in reachable[index]
    ]
    if cyclic_task_ids:
        raise ValueError(f"Cyclic precedence dependencies: {cyclic_task_ids[:10]}")

    edges: List[Tuple[int, int]] = []
    edge_types: List[int] = []
    unique_edges: Set[Tuple[int, int, int]] = set()

    def add_edge(source: int, target: int, relation: int) -> None:
        edge = (source, target, relation)
        if edge not in unique_edges:
            unique_edges.add(edge)
            edges.append((source, target))
            edge_types.append(relation)

    for predecessor_index, successors in enumerate(precedence):
        for successor_index in successors:
            add_edge(predecessor_index, successor_index, 0)
            add_edge(successor_index, predecessor_index, 1)
    for row in rows:
        task_id = int(row[0])
        task_index = id_to_index[task_id]
        for conflict_id in _parse_ids(row[10]):
            if conflict_id not in id_to_index:
                raise ValueError(
                    f"Task {task_id} references missing spatial-conflict task {conflict_id}."
                )
            if conflict_id == task_id:
                continue
            conflict_index = id_to_index[conflict_id]
            if (
                conflict_index in reachable[task_index]
                or task_index in reachable[conflict_index]
            ):
                continue
            add_edge(task_index, conflict_index, 2)
            add_edge(conflict_index, task_index, 2)
    if not edges:
        raise ValueError(f"{csv_path.name} produced an empty graph.")
    edge_index = torch.tensor(edges, dtype=torch.long).t().contiguous()
    edge_type = torch.tensor(edge_types, dtype=torch.long)
    return edge_index, edge_type


class RelationalGraphConvolution(nn.Module):
    """Relation-aware mean message passing implemented with core PyTorch ops.

    This is the dense-parameter form of ``torch_geometric.nn.RGCNConv`` used by
    the original project: each relation has its own transform, messages are
    mean-aggregated at the target node, and a root transform plus bias is added.
    The fixed graph is materialized once as normalized relation adjacency
    matrices, so both training and Unity ONNX export use only matrix products.
    """

    def __init__(
        self,
        input_dim: int,
        output_dim: int,
        edge_index: torch.Tensor,
        edge_type: torch.Tensor,
        task_count: int,
    ) -> None:
        super().__init__()
        relation_count = RELATION_COUNT
        self.relation_weights = nn.Parameter(
            torch.empty(relation_count, input_dim, output_dim)
        )
        self.root_weight = nn.Parameter(torch.empty(input_dim, output_dim))
        self.bias = nn.Parameter(torch.empty(output_dim))
        adjacency = torch.zeros(relation_count, task_count, task_count)
        for relation in range(relation_count):
            relation_edges = edge_index[:, edge_type == relation]
            adjacency[
                relation, relation_edges[1], relation_edges[0]
            ] = 1.0
        normalizers = adjacency.sum(dim=2, keepdim=True).clamp_min(1.0)
        self.register_buffer("relation_adjacency", adjacency / normalizers)
        self.reset_parameters()

    def reset_parameters(self) -> None:
        nn.init.xavier_uniform_(self.relation_weights)
        nn.init.xavier_uniform_(self.root_weight)
        nn.init.zeros_(self.bias)

    def forward(
        self,
        node_features: torch.Tensor,
        edge_index: torch.Tensor,
        edge_type: torch.Tensor,
    ) -> torch.Tensor:
        del edge_index, edge_type  # Topology is held by the registered buffer.
        batch_size, node_count, _ = node_features.shape
        transformed = torch.matmul(
            node_features.reshape(batch_size, 1, node_count, -1),
            self.relation_weights,
        )
        relation_messages = torch.matmul(
            self.relation_adjacency.reshape(1, RELATION_COUNT, node_count, node_count),
            transformed,
        )
        return (
            node_features @ self.root_weight
            + relation_messages.sum(dim=1)
            + self.bias
        )


class _RelationalTaskEncoder(nn.Module):
    def __init__(
        self,
        task_count: int,
        input_dim: int,
        hidden_dim: int,
        num_layers: int = 3,
    ) -> None:
        super().__init__()
        if input_dim != TASK_FEATURE_DIMENSION + AGENT_FEATURE_DIMENSION:
            raise ValueError(
                f"Graph observations must have "
                f"{TASK_FEATURE_DIMENSION + AGENT_FEATURE_DIMENSION} features; "
                f"received {input_dim}."
            )
        if num_layers < 1:
            raise ValueError("num_layers must be at least 1.")
        self.task_count = task_count
        self.hidden_dim = hidden_dim
        edge_index, edge_type = load_graph_topology(task_count)
        self.register_buffer("edge_index", edge_index.clone())
        self.register_buffer("edge_type", edge_type.clone())
        layers = [
            RelationalGraphConvolution(
                TASK_FEATURE_DIMENSION,
                hidden_dim,
                edge_index,
                edge_type,
                task_count,
            )
        ]
        layers.extend(
            RelationalGraphConvolution(
                hidden_dim,
                hidden_dim,
                edge_index,
                edge_type,
                task_count,
            )
            for _ in range(num_layers - 1)
        )
        self.convolutions = nn.ModuleList(layers)

    def _encode_nodes(self, observations: torch.Tensor) -> torch.Tensor:
        if observations.ndim != 3:
            raise ValueError(
                f"Expected graph observations [batch, tasks, features], got "
                f"{tuple(observations.shape)}."
            )
        batch_size, task_count, _ = observations.shape
        if task_count != self.task_count:
            raise ValueError(
                f"Encoder topology has {self.task_count} tasks, but observation has "
                f"{task_count}."
            )
        node_features = observations[:, :, :TASK_FEATURE_DIMENSION]
        edge_index = self.edge_index.to(device=observations.device)
        edge_type = self.edge_type.to(device=observations.device)
        edge_count = edge_index.shape[1]
        offsets = (
            torch.arange(
                batch_size,
                dtype=edge_index.dtype,
                device=observations.device,
            )
            .unsqueeze(1)
            .expand(batch_size, edge_count)
            .reshape(-1)
            * task_count
        )
        batched_edges = edge_index.repeat(1, batch_size) + offsets.unsqueeze(0)
        batched_edge_types = edge_type.repeat(batch_size)
        hidden = node_features
        for convolution in self.convolutions:
            hidden = torch.relu(convolution(hidden, batched_edges, batched_edge_types))
        return hidden


class GraphEncoder(_RelationalTaskEncoder):
    """Actor encoder that returns one embedding per task node."""

    def __init__(
        self,
        task_count: int,
        input_dim: int,
        hidden_dim: int,
        output_dim: int,
        num_layers: int = 3,
    ) -> None:
        super().__init__(task_count, input_dim, hidden_dim, num_layers)
        combined_node_dim = hidden_dim + TASK_FEATURE_DIMENSION
        self.attention = nn.Linear(combined_node_dim, hidden_dim)
        self.attention_score = nn.Linear(hidden_dim, 1)
        self.readout = nn.Linear(
            combined_node_dim * 2 + AGENT_FEATURE_DIMENSION,
            output_dim,
        )

    def forward(self, observations: torch.Tensor) -> torch.Tensor:
        hidden = self._encode_nodes(observations)
        raw_task_features = observations[:, :, :TASK_FEATURE_DIMENSION]
        node_context = torch.cat((hidden, raw_task_features), dim=-1)
        scores = self.attention_score(torch.tanh(self.attention(node_context)))
        weights = torch.softmax(scores, dim=1)
        graph_context = torch.sum(weights * node_context, dim=1, keepdim=True)
        graph_context = graph_context.expand(-1, self.task_count, -1)
        agent_features = observations[:, :, TASK_FEATURE_DIMENSION:]
        return self.readout(torch.cat((node_context, graph_context, agent_features), dim=-1))


class GraphEncoder4V(_RelationalTaskEncoder):
    """Critic encoder that returns one attention-pooled graph embedding."""

    def __init__(
        self,
        task_count: int,
        input_dim: int,
        hidden_dim: int,
        output_dim: int,
        num_layers: int = 3,
    ) -> None:
        super().__init__(task_count, input_dim, hidden_dim, num_layers)
        combined_node_dim = hidden_dim + TASK_FEATURE_DIMENSION
        self.attention = nn.Linear(combined_node_dim, hidden_dim)
        self.attention_score = nn.Linear(hidden_dim, 1)
        self.readout = nn.Linear(combined_node_dim, output_dim)

    def forward(self, observations: torch.Tensor) -> torch.Tensor:
        hidden = self._encode_nodes(observations)
        raw_task_features = observations[:, :, :TASK_FEATURE_DIMENSION]
        node_context = torch.cat((hidden, raw_task_features), dim=-1)
        scores = self.attention_score(torch.tanh(self.attention(node_context)))
        weights = torch.softmax(scores, dim=1)
        graph_context = torch.sum(weights * node_context, dim=1)
        return self.readout(graph_context)
