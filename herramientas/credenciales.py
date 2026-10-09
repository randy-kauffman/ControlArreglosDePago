"""Carga de credenciales para los clientes del puesto de trabajo (`dataverse_api.py`, `graph_api.py`).

Dos cosas distintas, a propósito:

- **Dónde están los secretos** lo dice `proyecto.json` (versionado, sin secretos): una variable de entorno que
  apunta al archivo (`PP_SECRETS_FILE`) y una lista de rutas por defecto (`~/projects/pp_secrets.env`,
  `local/pp_secrets.env`). La variable manda; si apunta a un archivo que no existe es un error, no se cae
  en silencio a otra ruta (cargaría las credenciales de otro entorno sin que nadie lo note).
- **Qué dicen** vive solo en ese archivo, que está FUERA del repositorio (o en `local/`, que no se versiona).

Nunca imprime un valor. Los mensajes de error nombran rutas y NOMBRES de clave, jamás el contenido: una
credencial que se vuelca a un log o a la salida de un agente ya es una credencial perdida.

Sin dependencias fuera de la librería estándar.
"""
import json
import os

RAIZ_HERRAMIENTAS = os.path.dirname(os.path.abspath(__file__))
RAIZ_PROYECTO = os.path.dirname(RAIZ_HERRAMIENTAS)
ARCHIVO_CONFIGURACION = os.path.join(RAIZ_HERRAMIENTAS, "proyecto.json")


class ErrorCredenciales(Exception):
    """No se pudo ubicar o leer el archivo de secretos, o falta una clave. El mensaje nunca incluye valores."""


def cargar_configuracion(ruta=ARCHIVO_CONFIGURACION):
    """El único archivo de configuración versionado del proyecto (`herramientas/proyecto.json`)."""
    with open(ruta, encoding="utf-8") as f:
        return json.load(f)


def _expandir(ruta):
    ruta = os.path.expanduser(ruta)
    return ruta if os.path.isabs(ruta) else os.path.join(RAIZ_PROYECTO, ruta)


def ruta_secretos(entorno=None, config=None, existe=os.path.exists):
    """La ruta del archivo de secretos: la variable de entorno indicada en la configuración si está definida; si no,
    la primera ruta por defecto que exista. `entorno`, `config` y `existe` se inyectan solo en las pruebas."""
    entorno = os.environ if entorno is None else entorno
    config = cargar_configuracion() if config is None else config
    cred = config["credenciales"]
    variable = cred["variable_entorno"]
    if entorno.get(variable):
        ruta = _expandir(entorno[variable])
        if not existe(ruta):
            raise ErrorCredenciales(f"la variable de entorno {variable} apunta a '{ruta}', que no existe")
        return ruta
    candidatas = [_expandir(r) for r in cred["rutas_por_defecto"]]
    for ruta in candidatas:
        if existe(ruta):
            return ruta
    raise ErrorCredenciales(f"no se encontró el archivo de secretos: se buscó en {candidatas}; "
                            f"o se define la variable de entorno {variable} con la ruta del archivo")


def leer_secretos(archivo=None, requeridas=()):
    """Devuelve `{clave: valor}` del archivo (`CLAVE=valor`, con `#` para comentarios, comillas y `export` opcionales).
    Si faltan claves de `requeridas` (o están vacías) lanza `ErrorCredenciales` nombrándolas, sin ningún valor."""
    ruta = archivo or ruta_secretos()
    datos = {}
    try:
        with open(ruta, encoding="utf-8") as f:
            for linea in f:
                linea = linea.strip()
                if linea.startswith("export "):
                    linea = linea[len("export "):]
                if linea and not linea.startswith("#") and "=" in linea:
                    clave, valor = linea.split("=", 1)
                    datos[clave.strip()] = valor.strip().strip('"').strip("'")
    except FileNotFoundError:
        raise ErrorCredenciales(f"no existe el archivo de secretos '{ruta}'") from None
    except (OSError, UnicodeDecodeError) as e:
        # Solo el tipo: el texto de un UnicodeDecodeError puede incluir los bytes que no se pudieron leer.
        raise ErrorCredenciales(f"no se pudo leer el archivo de secretos '{ruta}' ({type(e).__name__})") from None
    faltan = [k for k in requeridas if not datos.get(k)]
    if faltan:
        raise ErrorCredenciales(f"al archivo de secretos '{ruta}' le faltan estas claves: {faltan}")
    return datos
