# AiApiServer 本地服务设置

`AiApiServer/` 是一个 Flask Python 服务，提供图像处理（去背景等）能力。

## 环境要求

- Python 3.10+
- NVIDIA GPU + CUDA 12.x（推荐，CPU 模式亦可但速度较慢）

## 安装

```bash
cd AiApiServer
python -m venv venv
source venv/bin/activate
pip install -r requirements.txt
```

依赖较大（包含 PyTorch + ONNX），首次安装约 10–20 分钟。

## 启动

```bash
cd AiApiServer
source venv/bin/activate
python main.py
```

默认监听 `http://127.0.0.1:50051`。

## 可用端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/getconfig` | 获取所有已注册的模型列表（Interrogator / Editor / Translator） |
| GET | `/listmodelsbytype?name=<type>` | 按类型筛选模型（例如 `rmbg2`） |
| POST | `/editimage` | 执行图像编辑（去背景等），请求体中包含 Model 和 Image 数据 |
| POST | `/interrogateimage` | 执行图像标记/描述 |
| POST | `/getmodelparams` | 获取指定模型的参数配置 |

## BDTM 设置

1. 启动 AiApiServer 后保持运行
2. 打开 BDTM → 设置 → 填入 AiApi 地址（默认 `http://127.0.0.1:50051`）
3. 工具 → 去背景… → 连接检查 → 选择模型 → 执行
