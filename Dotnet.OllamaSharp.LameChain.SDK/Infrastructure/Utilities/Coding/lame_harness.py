import contextlib, io, json, os, sys, time, traceback

MAX_TEXT = 4000

def _jsonable(obj):
    try:
        import numpy as np
        if isinstance(obj, np.generic): return obj.item()
        if isinstance(obj, np.ndarray): return obj.tolist()
    except ImportError: 
        pass
    try:
        import pandas as pd
        if isinstance(obj, pd.DataFrame): 
            return obj.head(100).to_dict(orient="records")
        if isinstance(obj, pd.Series): 
            return obj.head(100).to_dict()
    except ImportError: 
        pass
    if isinstance(obj, (set, tuple)): 
        return list(obj)
    if hasattr(obj, "isoformat"): 
        return obj.isoformat()                                  # date / datetime
    return repr(obj)                                           # último recurso: nunca rompe

def run_harness(code: str, out_dir: str) -> str:
    os.makedirs(out_dir, exist_ok=True)
    before = set(os.listdir(out_dir))
    ns = {"__name__": "__main__", "OUTPUT_DIR": out_dir}       # namespace limpio por ejecución
    out, err = io.StringIO(), io.StringIO()
    env = {"success": False, "error": None}
    t0 = time.perf_counter()
    try:
        compiled = compile(code, "generated.py", "exec")       # SyntaxError -> no se ejecuta nada
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            exec(compiled, ns)
        env["success"] = True
    except BaseException as ex:                                # incluye SystemExit
        env["error"] = {"type": type(ex).__name__, "message": str(ex),
                        "traceback": traceback.format_exc(limit=6)[-MAX_TEXT:]}
    finally:
        with contextlib.suppress(Exception):
            import matplotlib.pyplot as plt; plt.close("all")

    result = ns.get("result")
    env.update({
        "result_type": type(result).__name__,                  # "float", "dict", "DataFrame", "NoneType"...
        "result": result,
        "stdout": out.getvalue()[-MAX_TEXT:],
        "stderr": err.getvalue()[-MAX_TEXT:],
        "artifacts": [{"path": os.path.join(out_dir, f),
                       "size_bytes": os.path.getsize(os.path.join(out_dir, f))}
                      for f in sorted(set(os.listdir(out_dir)) - before)],
        "duration_ms": round((time.perf_counter() - t0) * 1000),
    })
    try:
        return json.dumps(env, default=_jsonable, ensure_ascii=False, allow_nan=False)
    except ValueError:                                         # NaN/inf de pandas: System.Text.Json no los acepta
        env["result"] = repr(result)[:MAX_TEXT]
        return json.dumps(env, default=_jsonable, ensure_ascii=False)

if __name__ == "__main__":   # python -X utf8 lame_harness.py <code.py> <out_dir> <envelope.json>
    code_path, out_dir, envelope_path = sys.argv[1:4]
    with open(code_path, encoding="utf-8") as f:
        payload = run_harness(f.read(), out_dir)
    with open(envelope_path, "w", encoding="utf-8") as f:
        f.write(payload)