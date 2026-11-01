#!/usr/bin/env python3
import sys
import xml.etree.ElementTree as ET
from pack_game import verify_host_processes

verify_host_processes(ET.parse(sys.argv[1]).getroot())
print("Processos do painel/jogo e inicialização Mono independente validados.")
