"""Caso 5: el buzon 2 manda un correo NUEVO (no respuesta) al buzon 1 con el MISMO asunto que el del caso 1 (peor caso de
falso positivo), despues de que el caso 1 ya esta registrado y con Regarding. Se espera Regarding vacio."""
from lib import *
g = Graph(); est = cargar(); tag = est["tag"]
asunto = f"[SPIKE3] Caso 1 consulta arreglo de pago {tag}"
est["caso5"] = g.enviar_borrador(g.buzon[2], [g.buzon[1]], asunto, f"<p>Caso 5: correo nuevo, hilo nuevo, mismo asunto que el caso 1 ({tag}).</p>")
guardar(est); print({k: v for k, v in est["caso5"].items() if k != "gid"})
