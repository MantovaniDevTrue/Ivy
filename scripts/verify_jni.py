#!/usr/bin/env python3
"""Reject a MainActivity stub whose lifecycle callbacks have no dynamic JNI registration."""
from pathlib import Path
import re
import sys


def verify_source(source, required="n_onCreate"):
    if not re.search(r"mono\.android\.Runtime\.register\s*\(", source):
        raise ValueError("MainActivity sem Runtime.register: callbacks JNI não serão registrados.")
    if not re.search(r"Runtime\.register\s*\([^;]*__md_methods\s*\)", source, re.S):
        raise ValueError("Runtime.register não referencia a tabela __md_methods.")
    methods = set(re.findall(r"\bnative\s+\S+\s+(n_\w+)\s*\(", source))
    if required not in methods:
        raise ValueError(f"Callback {required} ausente do stub Java.")
    missing = [method for method in sorted(methods) if not re.search(
        r'"' + re.escape(method) + r":\([^\"]+", source)]
    if missing:
        raise ValueError("Callbacks sem mapeamento JNI: " + ", ".join(missing))
    return methods


def main():
    root = Path(sys.argv[1])
    name = sys.argv[2] if len(sys.argv) > 2 else "MainActivity"
    files = list(root.rglob(name + ".java"))
    if len(files) != 1:
        raise ValueError(f"Esperado um stub {name}.java; encontrados {len(files)}.")
    required = sys.argv[3] if len(sys.argv) > 3 else "n_onCreate"
    methods = verify_source(files[0].read_text(), required)
    print(f"Registro JNI de {name} validado: " + ", ".join(sorted(methods)))


if __name__ == "__main__":
    main()
