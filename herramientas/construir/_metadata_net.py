"""Lector mínimo de la metadata de un ensamblado .NET (ECMA-335), sin dependencias.

Existe para una sola pregunta de `paquete_plugins.py`: ¿qué tipos, con su nombre
COMPLETO (namespace + clase), define este `.dll`? Buscar el nombre de la clase
como texto dentro de los bytes no alcanza: en la metadata el namespace y el
nombre viven en dos entradas distintas del heap `#Strings` (dos cadenas
terminadas en NUL, no una sola), así que una búsqueda por texto confunde
`Otro.Namespace.GenerarFormatoApi` con `Sanic.Carp.Plugins.Api.GenerarFormatoApi`.
La tabla `TypeDef` guarda las dos referencias en la misma fila: esa es la
fuente.

Solo lee lo necesario: cabeceras PE, cabecera CLI, la raíz de la metadata, el
heap `#Strings` y las tablas `Module`, `TypeRef` y `TypeDef` (en ese orden, las
únicas antes de `TypeDef` cuyo ancho de fila hay que conocer). Un archivo que no
es un ensamblado .NET bien formado levanta `NoEsEnsambladoNet`.
"""
import struct


class NoEsEnsambladoNet(ValueError):
    """El archivo no es un ensamblado .NET legible (nativo, truncado o corrupto)."""


def _leer(crudo, formato, posicion):
    try:
        return struct.unpack_from(formato, crudo, posicion)
    except struct.error:
        raise NoEsEnsambladoNet("el archivo termina antes de lo que su cabecera declara") from None


def _alinear4(n):
    return (n + 3) & ~3


def _rva_a_posicion(secciones, rva):
    for direccion, tam_virtual, posicion, tam_en_disco in secciones:
        if direccion <= rva < direccion + max(tam_virtual, tam_en_disco):
            return rva - direccion + posicion
    raise NoEsEnsambladoNet(f"la dirección {rva:#x} no cae en ninguna sección")


def _cadena(crudo, inicio, limite):
    fin = crudo.find(b"\x00", inicio, limite)
    if fin < 0:
        raise NoEsEnsambladoNet("una cadena de la metadata no termina en NUL")
    return crudo[inicio:fin]


def _flujos_de_metadata(crudo, raiz):
    if _leer(crudo, "<I", raiz)[0] != 0x424A5342:
        raise NoEsEnsambladoNet("la metadata no empieza con la firma BSJB")
    largo_version = _leer(crudo, "<I", raiz + 12)[0]
    posicion = raiz + 16 + largo_version
    cantidad = _leer(crudo, "<H", posicion + 2)[0]
    posicion += 4
    flujos = {}
    for _ in range(cantidad):
        desplazamiento, tamano = _leer(crudo, "<II", posicion)
        nombre = _cadena(crudo, posicion + 8, len(crudo))
        flujos[nombre.decode("ascii", "replace")] = (raiz + desplazamiento, tamano)
        posicion += 8 + _alinear4(len(nombre) + 1)
    return flujos


def nombres_de_tipos(crudo):
    """El conjunto de nombres completos (`Namespace.Clase`) de los tipos definidos
    en el ensamblado. Un tipo sin namespace figura solo con su nombre."""
    if crudo[:2] != b"MZ":
        raise NoEsEnsambladoNet("no es un ejecutable PE (no empieza con MZ)")
    inicio_pe = _leer(crudo, "<I", 0x3C)[0]
    if crudo[inicio_pe:inicio_pe + 4] != b"PE\x00\x00":
        raise NoEsEnsambladoNet("no tiene la firma PE")
    coff = inicio_pe + 4
    cantidad_secciones = _leer(crudo, "<H", coff + 2)[0]
    tam_cabecera_opcional = _leer(crudo, "<H", coff + 16)[0]
    opcional = coff + 20
    magia = _leer(crudo, "<H", opcional)[0]
    if magia == 0x10B:
        directorios = opcional + 96
    elif magia == 0x20B:
        directorios = opcional + 112
    else:
        raise NoEsEnsambladoNet(f"cabecera opcional desconocida ({magia:#x})")

    secciones = []
    for i in range(cantidad_secciones):
        base = opcional + tam_cabecera_opcional + 40 * i
        tam_virtual, direccion, tam_en_disco, posicion = _leer(crudo, "<IIII", base + 8)
        secciones.append((direccion, tam_virtual, posicion, tam_en_disco))

    rva_cli = _leer(crudo, "<I", directorios + 14 * 8)[0]
    if rva_cli == 0:
        raise NoEsEnsambladoNet("no tiene cabecera CLI: es un ejecutable nativo, no un ensamblado .NET")
    rva_metadata = _leer(crudo, "<I", _rva_a_posicion(secciones, rva_cli) + 8)[0]
    flujos = _flujos_de_metadata(crudo, _rva_a_posicion(secciones, rva_metadata))
    if "#~" not in flujos or "#Strings" not in flujos:
        raise NoEsEnsambladoNet("la metadata no trae los flujos '#~' y '#Strings' (¿tablas sin comprimir?)")

    inicio_strings, tam_strings = flujos["#Strings"]
    inicio_tablas, _ = flujos["#~"]
    tamanos_de_heap = _leer(crudo, "B", inicio_tablas + 6)[0]
    validas = _leer(crudo, "<Q", inicio_tablas + 8)[0]
    posicion = inicio_tablas + 24
    filas = {}
    for numero in range(64):
        if validas >> numero & 1:
            filas[numero] = _leer(crudo, "<I", posicion)[0]
            posicion += 4
    if tamanos_de_heap & 0x40:
        posicion += 4

    ancho_cadena = 4 if tamanos_de_heap & 0x01 else 2
    ancho_guid = 4 if tamanos_de_heap & 0x02 else 2

    def indice(*tablas):
        return 4 if max(filas.get(t, 0) for t in tablas) >= 1 << 16 else 2

    def indice_codificado(bits_de_etiqueta, *tablas):
        return 4 if max(filas.get(t, 0) for t in tablas) >= 1 << (16 - bits_de_etiqueta) else 2

    ancho_module = 2 + ancho_cadena + 3 * ancho_guid
    # ResolutionScope = Module | ModuleRef | AssemblyRef | TypeRef
    ancho_typeref = indice_codificado(2, 0x00, 0x1A, 0x23, 0x01) + 2 * ancho_cadena
    # Extends = TypeDef | TypeRef | TypeSpec; FieldList -> Field; MethodList -> MethodDef
    ancho_extends = indice_codificado(2, 0x02, 0x01, 0x1B)
    ancho_typedef = 4 + 2 * ancho_cadena + ancho_extends + indice(0x04) + indice(0x06)

    posicion += filas.get(0, 0) * ancho_module + filas.get(1, 0) * ancho_typeref

    def texto(desplazamiento):
        if desplazamiento >= tam_strings:
            raise NoEsEnsambladoNet("una fila apunta fuera del heap '#Strings'")
        return _cadena(crudo, inicio_strings + desplazamiento, inicio_strings + tam_strings).decode("utf-8", "replace")

    formato_indice = "<I" if ancho_cadena == 4 else "<H"
    nombres = set()
    for i in range(filas.get(2, 0)):
        base = posicion + i * ancho_typedef + 4
        nombre = texto(_leer(crudo, formato_indice, base)[0])
        espacio = texto(_leer(crudo, formato_indice, base + ancho_cadena)[0])
        nombres.add(f"{espacio}.{nombre}" if espacio else nombre)
    return nombres
