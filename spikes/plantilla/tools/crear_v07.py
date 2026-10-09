#!/usr/bin/env python3
"""Genera F-NIC-0000052_v07_borrador.xlsx a partir de una COPIA de la v06 editando el XML del paquete.

Por que no openpyxl: al hacer load/save pierde printerSettings, customXml, calcChain y reescribe
todas las partes (ver RESULTADO.md, seccion A). Aca solo se tocan las partes que cambian; el resto
del paquete (logo, drawing, theme, styles, printerSettings, customXml, docProps) queda byte a byte igual.

Uso: crear_v07.py <v06.xlsx> <v07_borrador.xlsx>
"""
import copy
import re
import sys
import zipfile

from lxml import etree

NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
N = {"m": NS}
Q = lambda t: "{%s}%s" % (NS, t)

CAPACIDAD = 60
PRIMERA = 7                      # primera fila de cuotas
ULTIMA_V06 = 54                  # ultima fila de cuotas en v06 (48 cuotas)
FILA_TOTALES_V06 = 55
DESPLAZAMIENTO = CAPACIDAD - (ULTIMA_V06 - PRIMERA + 1)   # 12 filas
ULTIMA_V07 = PRIMERA + CAPACIDAD - 1                       # 66
FILA_TOTALES_V07 = FILA_TOTALES_V06 + DESPLAZAMIENTO       # 67

HOJA_FORMATO = "xl/worksheets/sheet1.xml"
HOJA_CONTROL = "xl/worksheets/sheet2.xml"


def parse(data):
    return etree.fromstring(data)


def serialize(root):
    return etree.tostring(root, xml_declaration=True, encoding="UTF-8", standalone=True)


def celda(root, ref):
    r = root.xpath("//m:c[@r='%s']" % ref, namespaces=N)
    if len(r) != 1:
        raise SystemExit("celda %s no encontrada de forma unica" % ref)
    return r[0]


def vaciar_celda(c):
    """Deja la celda vacia conservando r y s (StyleIndex)."""
    for hijo in list(c):
        c.remove(hijo)
    if "t" in c.attrib:
        del c.attrib["t"]


def desplazar_ref(ref, delta):
    m = re.fullmatch(r"([A-Z]+)(\d+)", ref)
    return "%s%d" % (m.group(1), int(m.group(2)) + delta)


def editar_control(data):
    root = parse(data)
    sheet_data = root.find(Q("sheetData"))
    filas = {int(f.get("r")): f for f in sheet_data.findall(Q("row"))}

    # 1. Quitar numeros de cuota preimpresos (A7:A54): queda la celda con su estilo, sin valor.
    for r in range(PRIMERA, ULTIMA_V06 + 1):
        vaciar_celda(celda(root, "A%d" % r))

    # 2. E4: quitar =TODAY() (formula y valor en cache). Conserva el estilo.
    vaciar_celda(celda(root, "E4"))

    # 3. Desplazar filas 55..66 -> 67..78 (de abajo hacia arriba no hace falta: se renumera in situ).
    for r in sorted(filas):
        if r >= FILA_TOTALES_V06:
            fila = filas[r]
            nuevo = r + DESPLAZAMIENTO
            fila.set("r", str(nuevo))
            for c in fila.findall(Q("c")):
                c.set("r", desplazar_ref(c.get("r"), DESPLAZAMIENTO))

    # 4. Totales: SUM sobre las 60 filas (C67 y E67). El valor en cache queda en 0 (plantilla sin datos).
    for col in ("C", "E"):
        c = celda(root, "%s%d" % (col, FILA_TOTALES_V07))
        f = c.find(Q("f"))
        assert f is not None and f.text == "SUM(%s7:%s42)" % (col, col), "formula inesperada en %s" % col
        f.text = "SUM(%s%d:%s%d)" % (col, PRIMERA, col, ULTIMA_V07)

    # 5. Filas nuevas 55..66: clon de la fila 54 (mismo formato de celda), sin numero de cuota.
    plantilla = filas[ULTIMA_V06]
    anterior = plantilla
    for r in range(ULTIMA_V06 + 1, ULTIMA_V07 + 1):
        nueva = copy.deepcopy(plantilla)
        nueva.set("r", str(r))
        for c in nueva.findall(Q("c")):
            c.set("r", re.sub(r"\d+$", str(r), c.get("r")))
        anterior.addnext(nueva)
        anterior = nueva

    # 6. Celdas combinadas que estaban debajo de los totales.
    mc = root.find(Q("mergeCells"))
    for m in mc.findall(Q("mergeCell")):
        ini, fin = m.get("ref").split(":")
        if int(re.search(r"\d+$", ini).group()) >= FILA_TOTALES_V06:
            m.set("ref", "%s:%s" % (desplazar_ref(ini, DESPLAZAMIENTO), desplazar_ref(fin, DESPLAZAMIENTO)))

    # 7. Dimension.
    dim = root.find(Q("dimension"))
    dim.set("ref", "A1:M%d" % (66 + DESPLAZAMIENTO))
    return serialize(root)


def editar_formato(data):
    root = parse(data)
    # B7: quitar =TODAY(); F7: quitar nombre precargado (el string se purga de sharedStrings).
    vaciar_celda(celda(root, "B7"))
    vaciar_celda(celda(root, "F7"))
    return root


def reindexar_strings(root, idx_borrado):
    """Tras purgar un shared string, baja en 1 los indices posteriores."""
    for c in root.xpath("//m:c[@t='s']", namespaces=N):
        v = c.find(Q("v"))
        i = int(v.text)
        assert i != idx_borrado, "un string purgado sigue referenciado"
        if i > idx_borrado:
            v.text = str(i - 1)


def editar_calcchain(data):
    root = parse(data)
    for c in list(root):
        ref, sheet = c.get("r"), c.get("i")
        if (ref, sheet) in (("E4", "18"), ("B7", "17")):      # ya no tienen formula
            root.remove(c)
        elif sheet == "18" and ref in ("C55", "E55"):
            c.set("r", desplazar_ref(ref, DESPLAZAMIENTO))
    return serialize(root)


def main(origen, destino):
    zin = zipfile.ZipFile(origen)
    partes = {i.filename: zin.read(i.filename) for i in zin.infolist()}
    orden = [i.filename for i in zin.infolist()]

    # --- sharedStrings: A4 (version), A12 (etiqueta), purga de "Roger Moran"
    sst = parse(partes["xl/sharedStrings.xml"])
    sis = sst.findall(Q("si"))
    textos = ["".join(si.itertext()) for si in sis]
    i_titulo = textos.index("F-NIC-0000052-Formato de Arreglo de Pagos Préstamos Versión 06")
    i_etiqueta = textos.index("Número de Préstamo:")
    i_roger = textos.index("Roger Moran")
    sis[i_titulo].find(Q("t")).text = "F-NIC-0000052-Formato de Arreglo de Pagos Préstamos Versión 07"
    sis[i_etiqueta].find(Q("t")).text = "Número de Préstamo / Tarjeta:"
    sst.remove(sis[i_roger])
    n = len(sst.findall(Q("si")))
    sst.set("count", str(n))
    sst.set("uniqueCount", str(n))

    formato = editar_formato(partes[HOJA_FORMATO])
    reindexar_strings(formato, i_roger)
    control = parse(editar_control(partes[HOJA_CONTROL]))
    reindexar_strings(control, i_roger)

    partes["xl/sharedStrings.xml"] = serialize(sst)
    partes[HOJA_FORMATO] = serialize(formato)
    partes[HOJA_CONTROL] = serialize(control)
    partes["xl/calcChain.xml"] = editar_calcchain(partes["xl/calcChain.xml"])

    with zipfile.ZipFile(destino, "w", zipfile.ZIP_DEFLATED) as zout:
        for nombre in orden:
            zi = zipfile.ZipInfo(nombre, date_time=(1980, 1, 1, 0, 0, 0))   # reproducible
            zi.compress_type = zipfile.ZIP_DEFLATED
            zout.writestr(zi, partes[nombre])


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
