"""Construye, en memoria, un ensamblado .NET MÍNIMO pero con metadata de verdad
(PE + cabecera CLI + flujos `#~` y `#Strings` con las tablas `Module` y
`TypeDef`), para probar `_metadata_net.py` y la verificación de tipos de
`paquete_plugins.py` sin un compilador. La forma coincide con la de un `.dll`
real (se validó el lector contra el de un paquete de plugins ya construido):
`TypeDef` guarda el nombre y el namespace como DOS cadenas distintas del heap
`#Strings`, que es justo lo que una búsqueda de texto no ve.
"""
import struct


def _alinear(datos, multiplo=4):
    return datos + b"\x00" * (-len(datos) % multiplo)


def dll_dotnet(tipos, nombre_modulo="Prueba.dll"):
    """`tipos` son nombres completos (`Namespace.Clase`); uno sin punto queda sin namespace."""
    cadenas = bytearray(b"\x00")

    def cadena(texto):
        if texto == "":
            return 0
        posicion = len(cadenas)
        cadenas.extend(texto.encode("utf-8") + b"\x00")
        return posicion

    definiciones = [("", "<Module>")]
    for t in tipos:
        espacio, _, nombre = t.rpartition(".")
        definiciones.append((espacio, nombre))

    fila_module = struct.pack("<HHHHH", 0, cadena(nombre_modulo), 0, 0, 0)
    filas_typedef = b"".join(
        struct.pack("<IHHHHH", 0x00100001, cadena(nombre), cadena(espacio), 0, 1, 1) for espacio, nombre in definiciones
    )
    validas = (1 << 0) | (1 << 2)
    tablas = (
        struct.pack("<IBBBBQQ", 0, 2, 0, 0, 1, validas, 0)
        + struct.pack("<II", 1, len(definiciones))
        + fila_module
        + filas_typedef
    )
    tablas = _alinear(tablas)
    cadenas = _alinear(bytes(cadenas))

    version = _alinear(b"v4.0.30319\x00")
    cabeceras_de_flujo = 8 + 4 + 8 + 12  # dos flujos: '#~' (nombre en 4 bytes) y '#Strings' (en 12)
    largo_raiz = 16 + len(version) + 4 + cabeceras_de_flujo
    raiz = (
        b"BSJB" + struct.pack("<HHI", 1, 1, 0) + struct.pack("<I", len(version)) + version
        + struct.pack("<HH", 0, 2)
        + struct.pack("<II", largo_raiz, len(tablas)) + b"#~\x00\x00"
        + struct.pack("<II", largo_raiz + len(tablas), len(cadenas)) + b"#Strings\x00\x00\x00\x00"
    )
    assert len(raiz) == largo_raiz
    metadata = raiz + tablas + cadenas

    rva_seccion, posicion_seccion = 0x2000, 0x200
    cabecera_cli = struct.pack("<IHHII", 72, 2, 5, rva_seccion + 72, len(metadata)) + b"\x00" * (72 - 16)
    seccion = _alinear(cabecera_cli + metadata, 0x200)

    dos = bytearray(64)
    dos[0:2] = b"MZ"
    struct.pack_into("<I", dos, 0x3C, 64)
    coff = struct.pack("<HHIIIHH", 0x14C, 1, 0, 0, 0, 224, 0x2102)
    opcional = bytearray(224)
    struct.pack_into("<H", opcional, 0, 0x10B)
    struct.pack_into("<II", opcional, 96 + 14 * 8, rva_seccion, 72)
    tabla_de_secciones = b".text\x00\x00\x00" + struct.pack(
        "<IIIIIIHHI", len(seccion), rva_seccion, len(seccion), posicion_seccion, 0, 0, 0, 0, 0x60000020
    )
    cabeceras = bytes(dos) + b"PE\x00\x00" + coff + bytes(opcional) + tabla_de_secciones
    return cabeceras + b"\x00" * (posicion_seccion - len(cabeceras)) + seccion
