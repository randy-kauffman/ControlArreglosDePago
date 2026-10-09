"""Envia por Graph los correos de entrada de los casos 1 y 6 (t0 comun). Guarda ids en estado.json."""
from lib import *
g = Graph(); est = cargar()
tag = time.strftime("%H%M%S")
b1, b2, b3 = g.buzon[1], g.buzon[2], g.buzon[3]
est["tag"] = tag
est["t0"] = utc_iso()
est["caso1"] = g.enviar_borrador(b2, [b1], f"[SPIKE3] Caso 1 consulta arreglo de pago {tag}",
    "<p>Hola, quiero consultar el estado de mi arreglo de pago. Cedula ficticia 0-000-0000.</p>")
est["caso6a"] = g.enviar_borrador(b3, [b1], f"[SPIKE3] Caso 6A adjuntos pdf y png {tag}",
    "<p>Adjunto un PDF y una imagen PNG.</p>",
    [adj_graph("comprobante.pdf", pdf_chico("Spike 3 comprobante"), "application/pdf"),
     adj_graph("foto.png", png_chico(), "image/png")])
est["caso6b"] = g.enviar_borrador(b3, [b1], f"[SPIKE3] Caso 6B imagen inline {tag}",
    '<p>Imagen inline:</p><p><img src="cid:logo1" width="16" height="16"></p><p>Fin.</p>',
    [adj_graph("logo.png", png_chico(16, 16, (30, 30, 200)), "image/png", inline=True, cid="logo1")])
guardar(est)
print(json.dumps(est, indent=1))
