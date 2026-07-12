#!/usr/bin/env bash
# Launch Bdtm.Avalonia with CUDA 12 libraries on LD_LIBRARY_PATH when available.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP="${BDTM_APP:-$ROOT/dist/linux-x64/Bdtm.Avalonia}"

if [[ ! -x "$APP" ]]; then
  echo "App not found: $APP" >&2
  echo "Run ./scripts/publish-linux.sh first, or set BDTM_APP." >&2
  exit 1
fi

append_lib_dirs() {
  local base="$1"
  [[ -d "$base" ]] || return 0
  while IFS= read -r d; do
    [[ -d "$d" ]] || continue
    case ":${LD_LIBRARY_PATH:-}:" in
      *":$d:"*) ;;
      *) export LD_LIBRARY_PATH="$d${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}" ;;
    esac
  done < <(find "$base" -type d -name lib 2>/dev/null)
}

# Explicit override: colon-separated list of lib dirs
if [[ -n "${BDTM_CUDA_LIB_DIRS:-}" ]]; then
  IFS=':' read -r -a _dirs <<< "$BDTM_CUDA_LIB_DIRS"
  for d in "${_dirs[@]}"; do
    [[ -d "$d" ]] || continue
    export LD_LIBRARY_PATH="$d${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
  done
fi

# Auto-detect common pip nvidia wheel trees on this machine
if [[ -z "${BDTM_SKIP_CUDA_AUTOLIB:-}" ]]; then
  candidates=(
    "$HOME/Projects/toolbox/model-train/AnimaLoraStudio/venv/lib/python3.12/site-packages/nvidia"
    "$HOME/Projects/toolbox/comfyui-image/venv/lib/python3.12/site-packages/nvidia"
    "$HOME/Projects/toolbox/llm-inference/venv/lib/python3.12/site-packages/nvidia"
    "$HOME/Projects/toolbox/model-train/MonadForge/.venv/lib/python3.13/site-packages/nvidia"
    "$HOME/Projects/toolbox/model-train/anima_lora_webui/.venv/lib/python3.13/site-packages/nvidia"
    /usr/local/cuda/lib64
    /opt/cuda/lib64
  )
  for c in "${candidates[@]}"; do
    if [[ -d "$c" ]]; then
      if [[ "$(basename "$c")" == "nvidia" ]] || [[ -d "$c/cublas" ]]; then
        append_lib_dirs "$c"
      else
        export LD_LIBRARY_PATH="$c${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
      fi
    fi
  done
fi

# Models: default to ~/.local/share/bdtm/Models (override with BDTM_MODELS_DIR)
USER_MODELS="${XDG_DATA_HOME:-$HOME/.local/share}/bdtm/Models"
mkdir -p "$USER_MODELS"
if [[ -z "${BDTM_MODELS_DIR:-}" ]]; then
  DIST_MODELS="$ROOT/dist/linux-x64/Models"
  if [[ ! -e "$USER_MODELS/SmilingWolf/wd-eva02-large-tagger-v3" && -e "$DIST_MODELS/SmilingWolf/wd-eva02-large-tagger-v3" ]]; then
    mkdir -p "$USER_MODELS/SmilingWolf"
    ln -sfn "$DIST_MODELS/SmilingWolf/wd-eva02-large-tagger-v3" \
      "$USER_MODELS/SmilingWolf/wd-eva02-large-tagger-v3" || true
  fi
  # also try common local caches
  if [[ ! -e "$USER_MODELS/SmilingWolf/wd-eva02-large-tagger-v3" ]]; then
    CAND="$HOME/Projects/toolbox/datasets/Tool/sd-image-sorter/data/models/wd14-tagger/wd-eva02-large-tagger-v3"
    if [[ -d "$CAND" ]]; then
      mkdir -p "$USER_MODELS/SmilingWolf"
      ln -sfn "$CAND" "$USER_MODELS/SmilingWolf/wd-eva02-large-tagger-v3" || true
    fi
  fi
  export BDTM_MODELS_DIR="$USER_MODELS"
fi

if [[ "${BDTM_DEBUG_EP:-}" == "1" ]]; then
  echo "APP=$APP"
  echo "BDTM_MODELS_DIR=$BDTM_MODELS_DIR"
  echo "LD_LIBRARY_PATH=$LD_LIBRARY_PATH" | tr ':' '\n' | head -30
  # quick library check
  for lib in libcublasLt.so.12 libcudart.so.12 libcudnn.so.9; do
    if ldconfig -p 2>/dev/null | grep -q "$lib"; then
      echo "system has $lib"
    elif find ${LD_LIBRARY_PATH//:/ } -name "$lib" 2>/dev/null | head -1 | grep -q .; then
      echo "LD path has $lib -> $(find ${LD_LIBRARY_PATH//:/ } -name "$lib" 2>/dev/null | head -1)"
    else
      echo "WARNING: $lib not found on LD_LIBRARY_PATH"
    fi
  done
fi

exec "$APP" "$@"
