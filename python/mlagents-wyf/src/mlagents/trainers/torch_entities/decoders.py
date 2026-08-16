from typing import List, Dict

from mlagents.torch_utils import torch, nn
from mlagents.trainers.torch_entities.layers import linear_layer


class ValueHeads(nn.Module):
    def __init__(self, stream_names: List[str], input_size: int, output_size: int = 1):
        super().__init__()
        self.stream_names = stream_names
        _value_heads = {}

        for name in stream_names:
            value = linear_layer(input_size, output_size)
            _value_heads[name] = value
        self.value_heads = nn.ModuleDict(_value_heads)

    def forward(self, hidden: torch.Tensor) -> Dict[str, torch.Tensor]:
        value_outputs = {}
        for stream_name, head in self.value_heads.items():
            value_outputs[stream_name] = head(hidden).squeeze(-1)
        return value_outputs

class TaskSharedValueHeads(nn.Module):  # 2025.09.27 wyf
    def __init__(self, stream_names: List[str], hidden_dim: int, num_nodes: int):
        super().__init__()
        self.stream_names = stream_names
        _value_heads = {}

        for name in stream_names:
            # 1. 节点级线性层：hidden_dim -> 1
            node_proj = nn.Linear(hidden_dim, 1)
            # 2. 汇聚线性层：num_nodes -> 1
            node_agg = nn.Linear(num_nodes, 1)
            _value_heads[name] = nn.ModuleDict({
                "proj": node_proj,
                "agg": node_agg
            })
        self.value_heads = nn.ModuleDict(_value_heads)

    def forward(self, hidden: torch.Tensor) -> Dict[str, torch.Tensor]:
        """
        hidden: [batch_size, num_nodes, hidden_dim]
        return: {stream_name: [batch_size]}
        """
        value_outputs = {}
        for stream_name, modules in self.value_heads.items():
            # Step 1: 节点级投影 (batch, num_nodes, hidden_dim) -> (batch, num_nodes, 1)
            node_values = modules["proj"](hidden)
            # Step 2: 转置 (batch, num_nodes, 1) -> (batch, 1, num_nodes)
            node_values = node_values.transpose(1, 2)
            # Step 3: 聚合线性 (batch, 1, num_nodes) -> (batch, 1, 1)
            value = modules["agg"](node_values)
            # Step 4: squeeze 成标量 (batch,)
            value_outputs[stream_name] = value.squeeze(-1).squeeze(-1)

        return value_outputs

class GraphLevelValueHeads(nn.Module):  # 2025.09.27 wyf
    def __init__(self, stream_names: List[str], hidden_dim: int):
        super().__init__()
        self.stream_names = stream_names
        _value_heads = {}

        for name in stream_names:
            # 图级线性层：hidden_dim -> 1
            value = linear_layer(hidden_dim, 1)
            _value_heads[name] = value
        self.value_heads = nn.ModuleDict(_value_heads)

    def forward(self, hidden: torch.Tensor) -> Dict[str, torch.Tensor]:
        """
        hidden: [batch_size, num_nodes, hidden_dim]
        return: {stream_name: [batch_size]}
        """
        # Step 1: mean pooling over nodes -> (batch_size, hidden_dim)
        graph_emb = hidden.mean(dim=1)

        # Step 2: 每个 stream 一个 value head (batch_size, 1) -> squeeze -> (batch_size,)
        value_outputs = {}
        for stream_name, head in self.value_heads.items():
            to_squeeze = head(graph_emb)
            value_outputs[stream_name] = to_squeeze.squeeze(-1)
        return value_outputs
