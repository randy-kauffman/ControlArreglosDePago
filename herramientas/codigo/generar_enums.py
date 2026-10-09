#!/usr/bin/env python3
"""Genera el archivo de enums de C# del proyecto: un `enum` por cada choice global
construido, con los valores EXACTOS de su playbook (BP-PP-193: el código nunca
usa el literal numérico).

    python3 herramientas/codigo/generar_enums.py            # escribe el archivo
    python3 herramientas/codigo/generar_enums.py --comprobar  # falla si está desactualizado

De dónde sale cada dato (nada del proyecto está escrito en este programa):

- La carpeta de playbooks, de la clave `playbooks` de `herramientas/proyecto.json`;
  los choices están en `<playbooks>/choice-global/*.md`.
- El archivo de destino y el namespace, de `codigo.enums_destino` y
  `codigo.enums_namespace` de ese mismo archivo.
- La abreviatura del proyecto (para reconocer `CH - <ABREV> - Nombre`) y los
  idiomas, del bloque json de `## 1. Identidad` de cada playbook.

Decisión sobre los nombres: el displayname y la etiqueta de cada opción son un
objeto por idioma (skill `power-platform-especificar` 0.2.0). El nombre del enum
(el `Nombre` de `CH - <ABREV> - Nombre`) y el de cada miembro se derivan SIEMPRE
del texto del PRIMER idioma de `idiomas` de la Identidad, en PascalCase. Es una
regla fija y no una elección por playbook para que un cambio de orden de
`idiomas` sea lo único que cambia los nombres del código. Es un error (el
generador no escribe nada) que el nombre resultante no sea un identificador de
C# válido, que dos opciones de un choice den el mismo nombre de miembro, que dos
choices den el mismo nombre de enum, o que falte alguno de los idiomas en un
displayname o en una etiqueta.
"""
import os
import re
import sys
from xml.sax.saxutils import escape

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(_AQUI))
sys.path.insert(0, os.path.join(os.path.dirname(_AQUI), "construir"))

from _comun import (  # noqa: E402
    ErrorPlaybook,
    dividir_secciones,
    leer_texto,
    obtener_componente,
    obtener_identidad,
    validar_textos_por_idioma,
)
from credenciales import RAIZ_PROYECTO, cargar_configuracion  # noqa: E402

TIPO = "choice-global"
IDENTIFICADOR = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")
NAMESPACE = re.compile(r"[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*")


def pascal(texto):
    """'Estado de version de plantilla' -> 'EstadoDeVersionDePlantilla'; 'Pago en USD' -> 'PagoEnUSD'.
    Un texto sin letras ni dígitos da '' (el que llama lo rechaza: no es un identificador)."""
    partes = [p for p in re.split(r"[^A-Za-z0-9]+", texto) if p]
    nombre = "".join(p[0].upper() + p[1:] for p in partes)
    return nombre if not nombre or not nombre[0].isdigit() else "_" + nombre


# ---------------------------------------------------------------------------
# Configuración
# ---------------------------------------------------------------------------
def _texto_de(config, ruta):
    """Valor de texto no vacío en `config` siguiendo `ruta` (p. ej. ('codigo', 'enums_destino')); si no, termina con un mensaje."""
    valor = config
    for clave in ruta:
        valor = valor.get(clave) if isinstance(valor, dict) else None
    nombre = ".".join(ruta)
    if not isinstance(valor, str) or not valor.strip():
        raise SystemExit(f"herramientas/proyecto.json: la clave '{nombre}' falta o no es un texto no vacío: {valor!r}")
    return valor


def _configuracion(config):
    return cargar_configuracion() if config is None else config


def carpeta_de_choices(config=None, raiz=None):
    config = _configuracion(config)
    return os.path.join(RAIZ_PROYECTO if raiz is None else raiz, _texto_de(config, ("playbooks",)), TIPO)


def destino(config=None, raiz=None):
    config = _configuracion(config)
    return os.path.join(RAIZ_PROYECTO if raiz is None else raiz, _texto_de(config, ("codigo", "enums_destino")))


def namespace(config=None):
    valor = _texto_de(_configuracion(config), ("codigo", "enums_namespace"))
    if not NAMESPACE.fullmatch(valor):
        raise SystemExit(f"herramientas/proyecto.json: 'codigo.enums_namespace' = {valor!r} no es un namespace de C# válido")
    return valor


def rutas_de_playbooks(config=None, raiz=None):
    """Los playbooks de choice global, ordenados por nombre de archivo (el orden de los enums en lo generado)."""
    carpeta = carpeta_de_choices(config, raiz)
    if not os.path.isdir(carpeta):
        return []
    return [os.path.join(carpeta, n) for n in sorted(os.listdir(carpeta)) if n.endswith(".md")]


# ---------------------------------------------------------------------------
# Generación
# ---------------------------------------------------------------------------
def _identificador(texto, que, archivo):
    nombre = pascal(texto)
    if not IDENTIFICADOR.fullmatch(nombre):
        raise SystemExit(f"{archivo}: {que} {texto!r} da {nombre!r}, que no es un identificador de C# válido")
    return nombre


def _bloque_de(ruta):
    """El enum de un playbook: (nombre del enum, texto C#)."""
    archivo = os.path.basename(ruta)
    try:
        secciones = dividir_secciones(leer_texto(ruta))
        c = obtener_componente(secciones, TIPO)
        ident = obtener_identidad(secciones)
        idiomas = ident["idiomas"]
        displayname = validar_textos_por_idioma(c.get("displayname"), idiomas, "'displayname'")
        opciones = c.get("opciones")
        if not isinstance(opciones, list) or not opciones:
            raise ErrorPlaybook(f"'opciones' tiene que ser una lista no vacía, no {opciones!r}")
        etiquetas = []
        for i, op in enumerate(opciones):
            if not isinstance(op, dict):
                raise ErrorPlaybook(f"opciones[{i}] tiene que ser un objeto, no {op!r}")
            valor = op.get("valor")
            if not isinstance(valor, int) or isinstance(valor, bool):
                raise ErrorPlaybook(f"opciones[{i}].valor tiene que ser un entero, no {valor!r}")
            etiquetas.append(validar_textos_por_idioma(op.get("etiqueta"), idiomas, f"opciones[{i}].etiqueta"))
    except ErrorPlaybook as e:
        raise SystemExit(f"{archivo}: {e}") from None
    except OSError as e:
        raise SystemExit(f"{archivo}: no se pudo leer el playbook: {e}") from None

    nombre = c.get("nombre")
    primero = str(idiomas[0])
    visible = displayname[primero]
    prefijo = f"CH - {ident['abrev'].upper()} - "
    if not visible.startswith(prefijo) or visible == prefijo:
        raise SystemExit(f"{archivo}: el displayname del idioma {primero} ({visible!r}) tiene que ser '{prefijo}Nombre'")
    nombre_enum = _identificador(visible[len(prefijo):], f"{nombre}: el nombre del enum, del displayname del idioma {primero},", archivo)

    miembros, vistos = [], set()
    for op, etiqueta in zip(opciones, etiquetas):
        texto = etiqueta[primero]
        m = _identificador(texto, f"{nombre}: el nombre de miembro, de la etiqueta del idioma {primero},", archivo)
        if m in vistos:
            raise SystemExit(f"{archivo}: {nombre}: dos opciones dan el mismo nombre de miembro {m!r}")
        vistos.add(m)
        miembros.append(f"        /// <summary>{escape(texto)}</summary>\n        {m} = {op['valor']},")
    return nombre_enum, (f"    /// <summary>Choice global <c>{nombre}</c> ({escape(visible)}).</summary>\n"
                         f"    public enum {nombre_enum}\n    {{\n" + "\n".join(miembros) + "\n    }")


def generar(config=None, raiz=None):
    config = _configuracion(config)
    # Toda la configuración se valida antes de leer un solo playbook: una clave que falta no se descubre a mitad de camino.
    carpeta_de_choices(config, raiz)
    destino(config, raiz)
    espacio = namespace(config)
    rutas = rutas_de_playbooks(config, raiz)
    if not rutas:
        raise SystemExit(f"no hay playbooks de choice global en {carpeta_de_choices(config, raiz)}: no hay nada que generar")
    bloques, enums = [], {}
    for ruta in rutas:
        nombre_enum, bloque = _bloque_de(ruta)
        if nombre_enum in enums:
            raise SystemExit(f"{os.path.basename(ruta)}: el enum {nombre_enum!r} ya lo da {enums[nombre_enum]}: dos choices no pueden tener el mismo nombre de enum")
        enums[nombre_enum] = os.path.basename(ruta)
        bloques.append(bloque)
    origen = _texto_de(config, ("playbooks",)).replace(os.sep, "/").strip("/")
    return (f"// <auto-generated>\n// Generado por herramientas/codigo/generar_enums.py desde {origen}/{TIPO}/.\n"
            "// NO editar a mano: cambia el playbook del choice y se vuelve a generar.\n// </auto-generated>\n"
            f"namespace {espacio}\n{{\n" + "\n\n".join(bloques) + "\n}\n")


def main(argv=None, config=None, raiz=None):
    """`config` y `raiz` se inyectan solo en las pruebas."""
    argv = sys.argv[1:] if argv is None else argv
    config = _configuracion(config)
    raiz = RAIZ_PROYECTO if raiz is None else raiz
    texto = generar(config, raiz)
    ruta = destino(config, raiz)
    nombre = os.path.basename(ruta)
    if "--comprobar" in argv:
        actual = ""
        if os.path.exists(ruta):
            with open(ruta, encoding="utf-8") as f:
                actual = f.read()
        if actual != texto:
            print(f"{nombre} está desactualizado: correr herramientas/codigo/generar_enums.py")
            return 1
        print(f"{nombre} está al día")
        return 0
    os.makedirs(os.path.dirname(ruta), exist_ok=True)
    with open(ruta, "w", encoding="utf-8") as f:
        f.write(texto)
    print("escrito", os.path.relpath(ruta, raiz))
    return 0


if __name__ == "__main__":
    sys.exit(main())
