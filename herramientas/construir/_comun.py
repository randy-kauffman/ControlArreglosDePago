"""Utilidades compartidas por las herramientas de `herramientas/construir/`.

Lee el playbook (formato común de `power-platform-especificar` §3, versión
que ya reemplazó la tabla de prosa de "## 1. Identidad" por un bloque
```json```): separa sus secciones `## N. Título`, y expone el único bloque
```json``` de cada sección que hace falta. **La herramienta nunca lee prosa
ni tablas del playbook, y nunca deduce un dato de otro** (por ejemplo, la
abreviatura a partir del nombre de la solución): todo lo que hace falta para
construir viene explícito en alguno de los dos bloques json.

Sin dependencias fuera de la librería estándar.
"""
import decimal
import json
import os
import re
import sys

# Claves exactas del bloque json de '## 1. Identidad' (formato común de la
# skill `power-platform-especificar`, §3) y su tipo esperado. Vale para
# cualquier tipo de playbook, no solo choice global.
CLAVES_IDENTIDAD = {
    "tipo_playbook": str,
    "version_skill": str,
    "proyecto": str,
    "inventario": str,
    "fase": int,
    "entorno_url": str,
    "solucion": str,
    "publisher": str,
    "prefijo": str,
    "abrev": str,
    "prefijo_opciones": int,
    # Lista de LCID, nunca un valor único (skill 0.2.0): como mínimo 1033 y el idioma base del cliente.
    "idiomas": list,
}

LCID_BASE_MINIMO = 1033


class ErrorPlaybook(Exception):
    """El playbook o alguno de sus bloques json está mal formado, incompleto,
    o no es del tipo que espera la herramienta. Nunca se llegó a tocar el
    entorno: ni siquiera se intentó construir el cliente de Dataverse."""


class Bloqueado(Exception):
    """Una precondición no se cumple (del playbook contra sí mismo, o contra
    lo que dice el entorno). Nunca se llegó a crear, modificar ni borrar
    nada."""


class ErrorEntorno(Exception):
    """No se pudo averiguar algo contra el entorno: una consulta de
    precondición o de verificación devolvió un HTTP inesperado, o devolvió
    200 con una forma inesperada (cuerpo que no es un objeto, o un campo que
    la consulta declara devolver —'value', 'RetrieveProvisionedLanguages',
    'MetadataId', etc.— ausente o de otro tipo del esperado). Nunca se llegó
    a determinar si la precondición se cumple o no: por eso nunca es
    `Bloqueado`, siempre termina en el estado `error`."""


_NOMBRE_TIPO = {int: "un entero", str: "un texto", bool: "un booleano", dict: "un objeto", list: "una lista", "numero": "un número"}


def _es_del_tipo(valor, tipo):
    if tipo is int:
        # En Python `bool` es subclase de `int`: True no es un entero válido.
        return isinstance(valor, int) and not isinstance(valor, bool)
    if tipo == "numero":
        # Entero o decimal del JSON de la plataforma (un MaxValue puede llegar como 100 o como 999999999.99); nunca un booleano.
        return isinstance(valor, (int, float)) and not isinstance(valor, bool)
    return isinstance(valor, tipo)


def _acotado(valor, largo=160):
    texto = repr(valor)
    return texto if len(texto) <= largo else texto[:largo] + "…"


def exigir_forma(valor, tipo, consulta, campo, no_vacio=False, permite_nulo=False):
    """Único punto donde se valida el TIPO de un valor leído de una respuesta
    del entorno, antes de usarlo para decidir algo. Devuelve el valor si
    cumple; si no, lanza `ErrorEntorno` con un mensaje uniforme que nombra la
    consulta, el campo, qué se esperaba y qué llegó (acotado en largo).

    `tipo` es `int`, `str`, `bool`, `dict`, `list` o `"numero"` (entero o decimal, nunca booleano); o una lista de un
    elemento (`[int]`, `[dict]`...) para "lista de", que valida CADA elemento
    y dice cuál falló. `no_vacio` aplica a textos y listas. `permite_nulo`
    acepta `None` (para lo que la plataforma devuelve nulo legítimamente).

    Regla: un valor ausente o de otro tipo con HTTP 200 es "no se pudo
    averiguar" (`error`), nunca una respuesta de negocio. Quien llama decide
    aparte qué vacíos SÍ son de negocio (p. ej. `value: []` en `solutions`)."""
    if valor is None and permite_nulo:
        return None
    lista_de = isinstance(tipo, list)
    base = list if lista_de else tipo
    esperado = f"una lista de {_NOMBRE_TIPO[tipo[0]][3:]}s" if lista_de else _NOMBRE_TIPO[base]

    def fallar(nombre_campo, se_esperaba, llego):
        raise ErrorEntorno(
            f"{consulta} devolvió 200 con forma inesperada: '{nombre_campo}' debía ser {se_esperaba} "
            f"y llegó {_acotado(llego)}"
        )

    if not _es_del_tipo(valor, base):
        fallar(campo, esperado, valor)
    if lista_de:
        for i, elemento in enumerate(valor):
            if not _es_del_tipo(elemento, tipo[0]):
                fallar(f"{campo}[{i}]", _NOMBRE_TIPO[tipo[0]], elemento)
    if no_vacio and not valor:
        fallar(campo, f"{esperado} no vacío", valor)
    return valor


def carpeta_playbooks(config=None, raiz=None):
    """Carpeta de los playbooks del proyecto: la clave `playbooks` de `herramientas/proyecto.json` (la única configuración versionada),
    resuelta desde la raíz del proyecto (`raiz` solo se inyecta en las pruebas). Las herramientas `muestra_*` y el generador de enums la
    usan en vez de escribirla en el código."""
    herramientas = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if herramientas not in sys.path:
        sys.path.insert(0, herramientas)
    from credenciales import RAIZ_PROYECTO, cargar_configuracion

    config = cargar_configuracion() if config is None else config
    playbooks = config.get("playbooks")
    if not isinstance(playbooks, str) or not playbooks.strip():
        raise ValueError(f"proyecto.json: la clave 'playbooks' (carpeta de playbooks, relativa a la raíz del proyecto) falta o no es un texto: {playbooks!r}")
    return os.path.join(RAIZ_PROYECTO if raiz is None else raiz, playbooks)


def leer_texto(ruta):
    with open(ruta, encoding="utf-8") as f:
        return f.read()


def dividir_secciones(texto):
    """Devuelve {numero: {"titulo": str, "cuerpo": str}} para cada encabezado
    `## N. Título` de nivel 2, con el cuerpo hasta el siguiente encabezado de
    nivel 2 (o el final del archivo)."""
    patron = re.compile(r"^##\s+(\d+)\.\s*(.+?)\s*$", re.MULTILINE)
    coincidencias = list(patron.finditer(texto))
    secciones = {}
    for i, m in enumerate(coincidencias):
        numero = m.group(1)
        inicio = m.end()
        fin = coincidencias[i + 1].start() if i + 1 < len(coincidencias) else len(texto)
        secciones[numero] = {"titulo": m.group(2), "cuerpo": texto[inicio:fin]}
    return secciones


def _extraer_bloques_json(cuerpo_seccion):
    return re.findall(r"```json\s*\n(.*?)```", cuerpo_seccion, re.DOTALL)


def obtener_bloque_json(secciones, numero, nombre_seccion):
    """Devuelve el único bloque ```json``` (ya parseado a dict) de la sección
    `numero`. Lanza ErrorPlaybook si la sección no existe, si no tiene
    exactamente un bloque, si el bloque no es JSON válido, o si no es un
    objeto."""
    if numero not in secciones:
        raise ErrorPlaybook(f"no se encontró la sección '## {numero}. {nombre_seccion}' en el playbook")
    bloques = _extraer_bloques_json(secciones[numero]["cuerpo"])
    if len(bloques) == 0:
        raise ErrorPlaybook(f"la sección '## {numero}. {nombre_seccion}' no tiene ningún bloque ```json```")
    if len(bloques) > 1:
        raise ErrorPlaybook(
            f"la sección '## {numero}. {nombre_seccion}' tiene {len(bloques)} bloques ```json```; "
            "tiene que haber exactamente uno"
        )
    try:
        # Los decimales se leen exactos (`Decimal`): un monto como 999999999.99 no puede pasar por `float`.
        datos = json.loads(bloques[0], parse_float=decimal.Decimal)
    except json.JSONDecodeError as e:
        raise ErrorPlaybook(f"el bloque json de la sección {numero} no es JSON válido: {e}") from e
    if not isinstance(datos, dict):
        raise ErrorPlaybook(f"el bloque json de la sección {numero} tiene que ser un objeto")
    return datos


def _validar_claves_y_tipos(datos, claves_esperadas, de_donde):
    claves = set(datos.keys())
    esperadas = set(claves_esperadas)
    if claves != esperadas:
        faltan = sorted(esperadas - claves)
        sobran = sorted(claves - esperadas)
        raise ErrorPlaybook(f"claves de {de_donde} inválidas: faltan {faltan or 'ninguna'}, sobran {sobran or 'ninguna'}")
    for clave, tipo in claves_esperadas.items():
        valor = datos[clave]
        if tipo is int:
            if not isinstance(valor, int) or isinstance(valor, bool):
                raise ErrorPlaybook(f"'{clave}' de {de_donde} tiene que ser un entero, no {valor!r}")
        elif tipo is str:
            if not isinstance(valor, str) or not valor.strip():
                raise ErrorPlaybook(f"'{clave}' de {de_donde} tiene que ser texto no vacío")
        elif tipo is list:
            if not isinstance(valor, list):
                raise ErrorPlaybook(f"'{clave}' de {de_donde} tiene que ser una lista, no {valor!r}")


def obtener_identidad(secciones):
    """Bloque json de '## 1. Identidad', con exactamente las claves de
    CLAVES_IDENTIDAD y sus tipos validados. No valida coherencia contra el
    entorno (eso es una precondición de cada herramienta, contra Dataverse)."""
    identidad = obtener_bloque_json(secciones, "1", "Identidad")
    _validar_claves_y_tipos(identidad, CLAVES_IDENTIDAD, "'## 1. Identidad'")
    _validar_idiomas(identidad["idiomas"])
    return identidad


def _validar_idiomas(idiomas):
    """`idiomas` es la lista de LCID con que se etiqueta todo nombre visible. Sin red: que sea una lista de enteros
    positivos, sin repetidos, y que incluya el 1033 (la skill exige como mínimo 1033 y el idioma base del cliente;
    que el idioma base del entorno esté incluido y que todos estén provisionados lo comprueba la precondición contra el entorno)."""
    donde = "'idiomas' de '## 1. Identidad'"
    if not idiomas:
        raise ErrorPlaybook(f"{donde} no puede estar vacía: tiene que traer como mínimo {LCID_BASE_MINIMO} y el idioma base del cliente")
    for i, lcid in enumerate(idiomas):
        if not isinstance(lcid, int) or isinstance(lcid, bool) or lcid <= 0:
            raise ErrorPlaybook(f"{donde}[{i}] tiene que ser un LCID entero positivo, no {lcid!r}")
    if len(set(idiomas)) != len(idiomas):
        raise ErrorPlaybook(f"{donde} tiene idiomas repetidos: {idiomas}")
    if LCID_BASE_MINIMO not in idiomas:
        raise ErrorPlaybook(f"{donde} = {idiomas} no incluye el {LCID_BASE_MINIMO}: la skill exige como mínimo el 1033 y el idioma base del cliente")


def obtener_componente(secciones, tipo_esperado):
    """Bloque json de '## 2. Qué se crea', confirmado del `tipo_esperado`.
    No valida las claves propias del componente: cada tipo de herramienta
    tiene su propio conjunto (ninguno de ellos trae ya `lcid`, que vive en
    Identidad)."""
    datos = obtener_bloque_json(secciones, "2", "Qué se crea")
    tipo = datos.get("tipo")
    if tipo != tipo_esperado:
        raise ErrorPlaybook(
            f"'tipo' es {tipo!r}, no {tipo_esperado!r}: esta herramienta no procesa este playbook"
        )
    return datos



def validar_textos_por_idioma(valor, idiomas, donde, sin_tildes=True):
    """Todo nombre visible se escribe UNA VEZ POR CADA LCID de `idiomas` (skill 0.2.0), nunca como cadena única:
    un objeto `{"1033": ..., "3082": ...}` con exactamente esas claves (ninguna de más, ninguna de menos) y un
    texto no vacío en cada una. Con `sin_tildes` (el caso de los nombres visibles, BP-PP-197) cada texto pasa por
    `exigir_sin_tildes`. Devuelve el objeto. Lanza `ErrorPlaybook`; el mensaje nombra el campo y el idioma."""
    if not isinstance(valor, dict):
        raise ErrorPlaybook(f"{donde} tiene que ser un objeto con un texto por cada idioma {idiomas}, no {valor!r}")
    esperadas = [str(i) for i in idiomas]
    faltan = [i for i in esperadas if i not in valor]
    sobran = sorted(str(k) for k in valor if k not in esperadas)
    if faltan or sobran:
        raise ErrorPlaybook(f"{donde} tiene que traer un texto por cada idioma {idiomas}: faltan {faltan or 'ninguno'}, sobran {sobran or 'ninguno'}")
    for i in esperadas:
        texto = valor[i]
        if not isinstance(texto, str) or not texto.strip():
            raise ErrorPlaybook(f"{donde}[{i}] tiene que ser texto no vacío, no {texto!r}")
        if sin_tildes:
            exigir_sin_tildes(texto, f"{donde}[{i}]")
    return valor


def etiqueta_web_api(textos_por_idioma, idiomas):
    """Un `Label` del Web API con una etiqueta por cada LCID de `idiomas`, en ese orden. `textos_por_idioma` es
    `{"1033": ..., "3082": ...}` (ya validado con `validar_textos_por_idioma`)."""
    return {
        "@odata.type": "Microsoft.Dynamics.CRM.Label",
        "LocalizedLabels": [
            {"@odata.type": "Microsoft.Dynamics.CRM.LocalizedLabel", "Label": textos_por_idioma[str(i)], "LanguageCode": i}
            for i in idiomas
        ],
    }


def etiqueta_uniforme(texto, idiomas):
    """Un `Label` con el MISMO texto en cada idioma: así se escribe una descripción (prosa, no se traduce)."""
    return etiqueta_web_api({str(i): texto for i in idiomas}, idiomas)


def exigir_etiqueta(etiqueta, consulta, campo, permite_nulo=False):
    """Forma de un `Label` del Web API: objeto con `LocalizedLabels`, lista
    de objetos con `LanguageCode` entero y `Label` texto."""
    if exigir_forma(etiqueta, dict, consulta, campo, permite_nulo=permite_nulo) is None:
        return
    locs = exigir_forma(etiqueta.get("LocalizedLabels"), [dict], consulta, f"{campo}.LocalizedLabels", permite_nulo=True)
    for i, ll in enumerate(locs or []):
        exigir_forma(ll.get("LanguageCode"), int, consulta, f"{campo}.LocalizedLabels[{i}].LanguageCode")
        exigir_forma(ll.get("Label"), str, consulta, f"{campo}.LocalizedLabels[{i}].Label", permite_nulo=True)


class Rastro:
    """Envuelve al cliente para recordar qué consulta estaba en curso: la red
    de último recurso la nombra, así un fallo imprevisto dice dónde saltó."""

    def __init__(self, dv):
        self._dv = dv
        self.en_curso = "la preparación de la primera consulta"

    @property
    def api(self):
        """URL absoluta del endpoint. Hace falta para los `@odata.id` de
        `$ref`: OData rechaza ahí un URI relativo."""
        return self._dv.api

    def call(self, metodo, ruta, *args, **kwargs):
        self.en_curso = f"{metodo} {ruta.split('?')[0]}"
        return self._dv.call(metodo, ruta, *args, **kwargs)

    def subir_archivo(self, ruta_columna, *args, **kwargs):
        self.en_curso = f"la subida del archivo a {ruta_columna}"
        return self._dv.subir_archivo(ruta_columna, *args, **kwargs)

    def descargar_archivo(self, ruta_columna, *args, **kwargs):
        self.en_curso = f"la descarga del archivo de {ruta_columna}"
        return self._dv.descargar_archivo(ruta_columna, *args, **kwargs)


C_SOLUCION = "la consulta de la solución (GET solutions)"
C_IDIOMA_BASE = "la consulta del idioma base del entorno (GET organizations)"
C_IDIOMAS = "la consulta de idiomas provisionados (GET RetrieveProvisionedLanguages)"


def comprobar_solucion_e_idioma(dv, identidad):
    """Precondiciones comunes a toda herramienta de construcción, contra el
    entorno: la solución existe, no es managed y su publisher coincide con el
    de Identidad por unique name, prefijo y prefijo de opciones; y cada idioma
    de `idiomas` está provisionado y el idioma base del entorno es uno de ellos. Devuelve el `solutionid`. Lanza
    `Bloqueado` si una precondición no se cumple y `ErrorEntorno` si no se
    pudo averiguar."""
    solucion = identidad["solucion"]
    ruta_sol = (
        "solutions?$select=uniquename,solutionid,ismanaged"
        "&$expand=publisherid($select=uniquename,customizationprefix,customizationoptionvalueprefix)"
        f"&$filter=uniquename eq '{solucion}'"
    )
    est, cuerpo, _ = dv.call("GET", ruta_sol)
    if est != 200:
        raise ErrorEntorno(f"{C_SOLUCION} '{solucion}' devolvió HTTP {est} (se esperaba 200): {cuerpo}")
    exigir_forma(cuerpo, dict, C_SOLUCION, "cuerpo")
    filas = exigir_forma(cuerpo.get("value"), [dict], C_SOLUCION, "value")
    if len(filas) != 1:
        # 'value' con otra cantidad de filas (incluida la lista vacía) SÍ es
        # una respuesta de negocio legítima: la solución no existe (o hay
        # más de una con el mismo unique name).
        raise Bloqueado(f"la solución '{solucion}' no existe o hay más de una fila ({len(filas)})")
    fila_sol = filas[0]
    # Primero la forma de TODO lo que se va a usar; recién después se decide.
    es_managed = exigir_forma(fila_sol.get("ismanaged"), bool, C_SOLUCION, "ismanaged")
    solution_id = exigir_forma(fila_sol.get("solutionid"), str, C_SOLUCION, "solutionid", no_vacio=True)
    publisher = exigir_forma(fila_sol.get("publisherid"), dict, C_SOLUCION, "publisherid")
    pub_nombre = exigir_forma(publisher.get("uniquename"), str, C_SOLUCION, "publisherid.uniquename")
    pub_prefijo = exigir_forma(publisher.get("customizationprefix"), str, C_SOLUCION, "publisherid.customizationprefix")
    pub_prefijo_opciones = exigir_forma(
        publisher.get("customizationoptionvalueprefix"), int, C_SOLUCION, "publisherid.customizationoptionvalueprefix"
    )
    if es_managed:
        raise Bloqueado(f"la solución '{solucion}' está managed; no se construye ahí")
    if pub_nombre != identidad["publisher"]:
        raise Bloqueado(f"el publisher de '{solucion}' es {pub_nombre!r}, el playbook espera {identidad['publisher']!r}")
    if pub_prefijo != identidad["prefijo"]:
        raise Bloqueado(f"el prefijo del publisher es {pub_prefijo!r}, el playbook espera {identidad['prefijo']!r}")
    if pub_prefijo_opciones != identidad["prefijo_opciones"]:
        raise Bloqueado(
            f"el prefijo de opciones del publisher es {pub_prefijo_opciones!r}, "
            f"el playbook espera {identidad['prefijo_opciones']!r}"
        )

    est, cuerpo, _ = dv.call("GET", "organizations?$select=languagecode")
    if est != 200:
        raise ErrorEntorno(f"{C_IDIOMA_BASE} devolvió HTTP {est} (se esperaba 200): {cuerpo}")
    # Todo entorno tiene exactamente una organización con su idioma base: acá
    # una lista vacía no es una respuesta de negocio, es forma inesperada.
    exigir_forma(cuerpo, dict, C_IDIOMA_BASE, "cuerpo")
    filas_org = exigir_forma(cuerpo.get("value"), [dict], C_IDIOMA_BASE, "value", no_vacio=True)
    lcid_base = exigir_forma(filas_org[0].get("languagecode"), int, C_IDIOMA_BASE, "languagecode")

    est, cuerpo, _ = dv.call("GET", "RetrieveProvisionedLanguages")
    if est != 200:
        raise ErrorEntorno(f"{C_IDIOMAS} devolvió HTTP {est} (se esperaba 200): {cuerpo}")
    # El idioma base siempre está provisionado: una lista vacía tampoco es
    # una respuesta de negocio.
    exigir_forma(cuerpo, dict, C_IDIOMAS, "cuerpo")
    provisionados = exigir_forma(
        cuerpo.get("RetrieveProvisionedLanguages"), [int], C_IDIOMAS, "RetrieveProvisionedLanguages", no_vacio=True
    )
    sin_provisionar = [i for i in identidad["idiomas"] if i not in provisionados]
    if sin_provisionar:
        raise Bloqueado(f"los idiomas {sin_provisionar} del playbook no están provisionados en el entorno (provisionados: {provisionados})")
    if lcid_base not in identidad["idiomas"]:
        raise Bloqueado(
            f"el idioma base del entorno ({lcid_base}) no está en los idiomas del playbook {identidad['idiomas']}: "
            "los nombres quedarían sin etiqueta en el idioma que se ve"
        )
    return solution_id


def leer_entorno(dv, ruta, consulta, admite_404=False):
    """GET contra el entorno con las reglas de estado de la receta: 404 es
    "no existe" solo donde se admite; cualquier otro HTTP distinto de 200 es
    `ErrorEntorno`; y el cuerpo tiene que ser un objeto. Devuelve el cuerpo,
    o `None` si fue un 404 admitido."""
    est, cuerpo, _ = dv.call("GET", ruta)
    if est == 404 and admite_404:
        return None
    if est != 200:
        raise ErrorEntorno(f"{consulta} devolvió HTTP {est} (se esperaba 200{' o 404' if admite_404 else ''}): {cuerpo}")
    return exigir_forma(cuerpo, dict, consulta, "cuerpo")


def exigir_unique_name_de_paquete(valor, campo):
    """El unique name de un paquete de plugins que un playbook nombra (`paquete`): letras y dígitos ASCII, guion bajo y
    punto, hasta 128. Va dentro de un `$filter` de OData: una comilla lo rompería. Por rango de caracteres, no con
    `str.isalnum()`, que acepta letras de cualquier alfabeto."""
    if not valor or len(valor) > 128 or not all(
        ("a" <= ch <= "z") or ("A" <= ch <= "Z") or ("0" <= ch <= "9") or ch in "_." for ch in valor
    ):
        raise ErrorPlaybook(
            f"{campo} = {valor!r} no es un unique name de paquete de plugins: solo letras y dígitos ASCII, guion bajo y "
            "punto, hasta 128 caracteres"
        )


def fuera_de_la_solucion(que, solucion, solution_id):
    """La diferencia "el componente no está en la solución", dicha de forma que se entienda qué hacer. Esta herramienta
    todavía no agrega componentes a una solución (AddSolutionComponent se ensaya primero en Dev con descartables)."""
    return (
        f"{que} no figura como componente de la solución '{solucion}' (solutionid {solution_id}); esta herramienta "
        "todavía no agrega componentes a una solución (pendiente: AddSolutionComponent). Hay que agregarlo a mano a la "
        "solución y volver a verificar"
    )


C_PAQUETE_DE_PLUGINS = "la consulta del paquete de plugins (GET pluginpackages)"
C_ENSAMBLADOS_DEL_PAQUETE = "la consulta de los ensamblados del paquete (GET pluginassemblies)"
C_TIPOS_DE_PLUGIN = "la consulta del tipo de plugin (GET plugintypes)"


class TiposDelPaquete:
    """Los `plugintype` que pertenecen a UN paquete de plugins, por su unique name.

    Un step o una Custom API apunta a un tipo de plugin. Resolverlo por su `typename` a secas es un error esperando
    a pasar: en el entorno de desarrollo hay paquetes de spikes que registran las mismas clases, y el step quedaría
    apuntando a un binario que no es el del proyecto sin que nada lo avise. Acá un tipo existe solo si lo trajo el
    paquete declarado: paquete -> sus ensamblados (`_packageid_value`) -> los tipos de cada ensamblado
    (`_pluginassemblyid_value`). Cada consulta se hace UNA vez por instancia.

    Qué es de negocio y qué es técnico, según el contrato: el paquete que no existe y el tipo que el paquete no trae
    son `Bloqueado` (la consulta respondió bien: falta registrar el paquete); un HTTP no previsto, una forma
    inesperada, dos paquetes con el mismo unique name o un `typename` repetido dentro del paquete son `ErrorEntorno`."""

    def __init__(self, dv, uniquename):
        self._dv = dv
        self.uniquename = uniquename
        self._tipos = None
        self._repetidos = set()

    def _cargar(self):
        ruta = f"pluginpackages?$select=pluginpackageid,uniquename&$filter=uniquename eq '{self.uniquename}'"
        filas = exigir_forma(leer_entorno(self._dv, ruta, C_PAQUETE_DE_PLUGINS).get("value"), [dict], C_PAQUETE_DE_PLUGINS, "value")
        if len(filas) > 1:
            raise ErrorEntorno(
                f"{C_PAQUETE_DE_PLUGINS} devolvió {len(filas)} filas para el unique name '{self.uniquename}'; se esperaba a lo sumo una"
            )
        if not filas:
            raise Bloqueado(
                f"el paquete de plugins '{self.uniquename}' no está registrado en el entorno; "
                "primero hay que registrarlo con `herramientas/construir/paquete_plugins.py`"
            )
        paquete_id = exigir_forma(filas[0].get("pluginpackageid"), str, C_PAQUETE_DE_PLUGINS, "pluginpackageid", no_vacio=True)

        ruta = f"pluginassemblies?$select=pluginassemblyid&$filter=_packageid_value eq {paquete_id}"
        ensamblados = exigir_forma(
            leer_entorno(self._dv, ruta, C_ENSAMBLADOS_DEL_PAQUETE).get("value"), [dict], C_ENSAMBLADOS_DEL_PAQUETE, "value"
        )
        tipos, repetidos = {}, set()
        for asm in ensamblados:
            asm_id = exigir_forma(asm.get("pluginassemblyid"), str, C_ENSAMBLADOS_DEL_PAQUETE, "pluginassemblyid", no_vacio=True)
            ruta = f"plugintypes?$select=plugintypeid,typename&$filter=_pluginassemblyid_value eq {asm_id}"
            for t in exigir_forma(leer_entorno(self._dv, ruta, C_TIPOS_DE_PLUGIN).get("value"), [dict], C_TIPOS_DE_PLUGIN, "value"):
                nombre = exigir_forma(t.get("typename"), str, C_TIPOS_DE_PLUGIN, "typename", no_vacio=True)
                if nombre in tipos:
                    repetidos.add(nombre)
                tipos[nombre] = exigir_forma(t.get("plugintypeid"), str, C_TIPOS_DE_PLUGIN, "plugintypeid", no_vacio=True)
        self._tipos, self._repetidos = tipos, repetidos

    def todos(self):
        """{typename: plugintypeid} de todo lo que trajo el paquete."""
        if self._tipos is None:
            self._cargar()
        return dict(self._tipos)

    def id_del_tipo(self, typename):
        if self._tipos is None:
            self._cargar()
        if typename in self._repetidos:
            raise ErrorEntorno(
                f"{C_TIPOS_DE_PLUGIN} devolvió 2 filas para '{typename}' dentro del paquete '{self.uniquename}'; se esperaba una"
            )
        if typename not in self._tipos:
            raise Bloqueado(
                f"el tipo de plugin '{typename}' no está registrado en el paquete '{self.uniquename}' (un tipo con el mismo "
                "nombre en otro paquete no cuenta); primero hay que registrar el paquete con `herramientas/construir/paquete_plugins.py`"
            )
        return self._tipos[typename]


def valor_administrado(objeto, tipo, consulta, campo):
    """Valor de una propiedad administrada del Web API (`{Value, CanBeChanged, …}`)."""
    exigir_forma(objeto, dict, consulta, campo)
    return exigir_forma(objeto.get("Value"), tipo, consulta, f"{campo}.Value")


def textos_del_entorno(etiqueta):
    """{LCID: texto} de un `Label` ya validado en su forma. Con dos etiquetas del mismo idioma vale la última."""
    return {ll["LanguageCode"]: ll.get("Label") for ll in (etiqueta or {}).get("LocalizedLabels", []) or []}


def comparar_etiquetas(etiqueta, idiomas, que, esperado, difs, consulta, campo, permite_nulo=False):
    """Valida la forma de un `Label` y agrega a `difs` cada diferencia contra `esperado` (`{"1033": ..., "3082": ...}`),
    en LOS DOS SENTIDOS: un idioma de `idiomas` cuya etiqueta falta o difiere, y una etiqueta en un idioma que el
    playbook no declara (de más). Una etiqueta ausente se lee como texto vacío."""
    exigir_etiqueta(etiqueta, consulta, campo, permite_nulo=permite_nulo)
    reales = textos_del_entorno(etiqueta)
    for lcid in idiomas:
        texto = reales.get(lcid)
        if (texto or "") != esperado[str(lcid)]:
            difs.append(f"{que}[{lcid}]: entorno={texto!r} playbook={esperado[str(lcid)]!r}")
    de_mas = sorted(set(reales) - set(idiomas))
    if de_mas:
        difs.append(f"{que} tiene etiquetas en un idioma que el playbook no declara: {de_mas}")


def comparar_descripcion(etiqueta, idiomas, que, texto, difs, consulta, campo, permite_nulo=True):
    """Una descripción es prosa y se escribe con el mismo texto en cada idioma (ver `etiqueta_uniforme`)."""
    comparar_etiquetas(etiqueta, idiomas, que, {str(i): texto for i in idiomas}, difs, consulta, campo, permite_nulo=permite_nulo)


def nivel_requerido(requerida):
    return {"Value": "ApplicationRequired" if requerida else "None", "CanBeChanged": True,
            "ManagedPropertyLogicalName": "canmodifyrequirementlevelsettings"}


def auditoria_administrada(activa):
    return {"Value": activa, "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyauditsettings"}


ESPERA_BLOQUEO_SEGUNDOS = 30
REINTENTOS_BLOQUEO = 10


def _es_bloqueo_pasajero(respuesta):
    """Los dos textos con los que la plataforma dice "hay otra operación de
    metadatos o de soluciones en curso". La segunda forma aparece también
    cuando Microsoft instala sola una actualización de plataforma."""
    texto = str(respuesta)
    return "CustomizationLockException" in texto or "running at this moment" in texto


def escribir_metadatos(dv, metodo, ruta, cuerpo, dormir=None, **kw):
    """Toda escritura de metadatos de las herramientas pasa por acá. Dataverse
    admite UNA sola personalización de metadatos a la vez en el entorno: si
    otra está corriendo (otra herramienta, un ensayo, un borrado, alguien en
    el portal, o una actualización que Microsoft instala sola), responde `429`
    con `CustomizationLockException` o con "… running at this moment". Es pasajero:
    se espera y se reintenta, hasta `REINTENTOS_BLOQUEO` veces. Cualquier otra
    respuesta, incluido otro 429, se devuelve tal cual, sin reintentar."""
    if dormir is None:
        import time

        dormir = time.sleep
    for intento in range(REINTENTOS_BLOQUEO + 1):
        est, resp, cab = dv.call(metodo, ruta, cuerpo, **kw)
        if est != 429 or not _es_bloqueo_pasajero(resp) or intento == REINTENTOS_BLOQUEO:
            return est, resp, cab
        print(f"El entorno tiene otra personalización en curso; espero {ESPERA_BLOQUEO_SEGUNDOS} s y reintento ({intento + 1}/{REINTENTOS_BLOQUEO}).", flush=True)
        dormir(ESPERA_BLOQUEO_SEGUNDOS)


# Excepciones a BP-PP-197 aprobadas una por una (D-10). La lista está fijada en una prueba: no crece sola.
with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "excepciones_nombres.json"), encoding="utf-8") as _f:
    EXCEPCIONES_NOMBRES = json.load(_f)


def exigir_sin_tildes(texto, campo):
    """BP-PP-197: ningún nombre visible lleva tildes, ñ ni otro carácter fuera
    de ASCII. El nombre visible viaja por filtros de URL, XML de la solución,
    scripts y nombres de archivo; y en un componente sin nombre lógico ES el
    identificador. Las descripciones son prosa y no pasan por acá."""
    malos = sorted({c for c in texto if ord(c) > 126 or ord(c) < 32})
    if malos and texto not in EXCEPCIONES_NOMBRES["nombres_visibles"]:
        raise ErrorPlaybook(f"{campo} = {texto!r} lleva caracteres fuera de ASCII ({' '.join(malos)}): los nombres visibles van sin tildes ni ñ (BP-PP-197); "
                            "se escribe la palabra sin la tilde, no se la cambia por otra")


def nombre_de_archivo(texto):
    """Nombre de archivo para un componente que no tiene nombre lógico (un rol,
    un perfil): su nombre visible en minúscula, sin tildes y con guiones bajos.
    Se translitera en vez de descartar: «técnico» es `tecnico`, no `t_cnico`."""
    import re
    import unicodedata

    sin_marcas = "".join(c for c in unicodedata.normalize("NFD", texto.lower()) if unicodedata.category(c) != "Mn")
    return re.sub(r"[^a-z0-9]+", "_", sin_marcas).strip("_")


ESTADOS_DE_EXITO = ("creado", "actualizado", "ya_existia")


def estado_de_escritura(hubo_alta, hubo_escritura):
    """El estado de éxito según lo que se escribió (aprobado el 2026-10-01): 'creado' si se DIO DE ALTA algo que no
    existía, 'actualizado' si solo se corrigió algo que ya existía, 'ya_existia' si no se escribió nada. Antes las
    herramientas decían 'creado' también al actualizar, y un agente no podía saber si había un componente duplicado."""
    if hubo_alta:
        return "creado"
    return "actualizado" if hubo_escritura else "ya_existia"


def salida(estado, componente, detalle):
    """Imprime la última línea del contrato (JSON de una sola línea) y
    devuelve el código de salida: 0 solo para los estados de éxito
    ('creado', 'actualizado', 'ya_existia')."""
    print(json.dumps({"estado": estado, "componente": componente, "detalle": detalle}, ensure_ascii=False))
    return 0 if estado in ESTADOS_DE_EXITO else 1
