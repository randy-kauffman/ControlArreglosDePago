#!/usr/bin/env python3
"""Construye UNA vista de Dataverse (`savedquery`) desde un playbook de tipo
`vista`. Contrato: `power-platform-construir`; Learn, "Customize views".

    python3 herramientas/construir/vista.py <playbook.md> [--solo-verificar] [--completar]

El playbook **no lleva XML escrito a mano**: declara columnas, orden y filtro,
y la herramienta genera el `fetchxml` y el `layoutxml`. Es a propósito. Esos
dos XML tienen que estar de acuerdo entre sí (cada `<cell>` del layout necesita
su atributo en el fetch); escribirlos a mano en 17 playbooks es garantía de que
tarde o temprano se despeguen y la vista salga con una columna en blanco.

Una vista existente se reconoce por su `name` + `returnedtypecode`: Dataverse
no tiene clave alternativa para `savedquery`, así que ese par es la clave de
negocio. Dos vistas con el mismo nombre en la misma tabla son forma inesperada.

**Filtros** (`filtro`): `{tipo, condiciones, filtros, enlaces, buscar}`. `filtros` son grupos anidados `{tipo, condiciones, filtros}`
(hasta 3 niveles). Cada `enlace` lleva `tipo`: `inner` (por omisión, con `alias` y columnas) o `any` / `not any` (filtran por
filas relacionadas, sin alias ni columnas; Learn, "Filter rows using FetchXML", los documenta DENTRO de un `<filter>` y siguen
su `type`, así que se emiten ahí). Operadores de fecha: `yesterday`, `today` (sin valor), `olderthan-x-days` (entero >= 0),
`on-or-before`, `on-or-after`. La búsqueda rápida (`clase: BusquedaRapida`, querytype 4) exige `buscar: true`, `tipo: or` y
condiciones `like` con `["{0}"]`, y emite `<filter type="or" isquickfindfields="1">`; NUNCA se crea (toda tabla nace con la suya):
se repara con `--completar` y una segunda búsqueda rápida sobrante en la tabla se denuncia.

Learn: una personalización se publica sola al CREARSE; hay que publicar
explícitamente después de ACTUALIZARLA. La herramienta publica solo cuando
escribió un `PATCH`.

Última línea de la salida: JSON de una línea con `estado`
(creado | actualizado | ya_existia | difiere | bloqueado | error), `componente`, `detalle`.
"""
import os
import sys
from xml.sax.saxutils import escape, quoteattr

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

from _comun import (  # noqa: E402
    estado_de_escritura,
    Bloqueado,
    ErrorEntorno,
    ErrorPlaybook,
    Rastro,
    comprobar_solucion_e_idioma,
    dividir_secciones,
    escribir_metadatos,
    exigir_forma,
    exigir_sin_tildes,
    leer_entorno,
    leer_texto,
    obtener_componente,
    obtener_identidad,
    salida,
)

TIPO = "vista"

# Learn, tabla SavedQuery (verificado 2026-09-22). Son los cinco documentados.
CLASES = {"Publica": 0, "BusquedaAvanzada": 1, "Relacionada": 2, "BusquedaRapida": 4, "Lookup": 64}

# Operadores de FetchXML que hacen falta para las vistas del proyecto. Lista
# cerrada a propósito: un operador mal escrito no falla al crear la vista,
# falla al abrirla.
#
# `yesterday`, `today` y `olderthan-x-days` los pide `diseno/07` §3.4 (vistas 12.6 y 12.7). El spike 5 probó que NINGÚN
# operador da "fecha anterior a hoy" por sí solo sobre una columna DateOnly: la condición es `olderthan-x-days 1` OR
# `yesterday` (`spikes/plataforma/RESULTADO.md`, D-11). `olderthan-x-days` lleva un entero >= 0 (días).
OPERADORES_SIN_VALOR = {"null", "not-null", "eq-userid", "ne-userid", "yesterday", "today"}
OPERADORES_UN_VALOR = {"eq", "ne", "gt", "ge", "lt", "le", "like", "begins-with",
                       "olderthan-x-days", "on-or-before", "on-or-after"}
OPERADORES_VARIOS_VALORES = {"in", "not-in"}
OPERADORES_ENTEROS = {"olderthan-x-days"}
OPERADORES = OPERADORES_SIN_VALOR | OPERADORES_UN_VALOR | OPERADORES_VARIOS_VALORES

# Tipos de `link-entity`. `any` / `not any` (Learn, "Filter rows using FetchXML") filtran por filas relacionadas SIN devolver
# columnas ni repetir la fila padre: no llevan alias y no se pueden mostrar columnas de ellos. La forma probada en Dev (spike
# s52_q5_vista.py, vista guardada publicada y ejecutada) es el `link-entity` hijo de `<entity>`, sin alias, con su propio `<filter>`.
TIPOS_ENLACE = ("inner", "any", "not any")
TIPOS_ENLACE_DE_FILTRO = ("any", "not any")
PROFUNDIDAD_MAXIMA_DE_FILTROS = 3
VALOR_DE_BUSQUEDA = "{0}"

ANCHO_POR_DEFECTO = 150
LARGO_NOMBRE = 200

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)

CLAVES = {"tipo": str, "nombre": str, "tabla": str, "clase": str, "descripcion": str,
          "pordefecto": bool, "columnas": list, "orden": list, "filtro": dict}

C_VISTA = "la consulta de la vista (GET savedqueries)"
C_BUSQUEDAS = "la consulta de las vistas de búsqueda rápida (GET savedqueries)"
C_TABLA = "la consulta de la definición de la tabla (GET EntityDefinitions)"
C_COMPONENTES = "la consulta de pertenencia a la solución (GET solutioncomponents)"


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def _validar_condiciones(condiciones, donde):
    for i, c in enumerate(condiciones):
        aqui = f"{donde}.condiciones[{i}]"
        exigir_forma(c, dict, aqui, "condicion")
        faltan = sorted({"columna", "operador"} - set(c))
        sobran = sorted(set(c) - {"columna", "operador", "valores"})
        if faltan or sobran:
            raise ErrorPlaybook(f"{aqui}: faltan {faltan}, sobran {sobran}; se admite {{columna, operador, valores}}")
        exigir_forma(c["columna"], str, aqui, "columna", no_vacio=True)
        operador = exigir_forma(c["operador"], str, aqui, "operador", no_vacio=True)
        if operador not in OPERADORES:
            raise ErrorPlaybook(f"{aqui}: operador {operador!r} no admitido; los válidos son {sorted(OPERADORES)}")
        valores = c.get("valores", [])
        exigir_forma(valores, list, aqui, "valores")
        if operador in OPERADORES_SIN_VALOR and valores:
            raise ErrorPlaybook(f"{aqui}: el operador {operador!r} no lleva valores y el playbook trae {len(valores)}")
        if operador in OPERADORES_UN_VALOR and len(valores) != 1:
            raise ErrorPlaybook(f"{aqui}: el operador {operador!r} lleva exactamente un valor, y trae {len(valores)}")
        if operador in OPERADORES_VARIOS_VALORES and not valores:
            raise ErrorPlaybook(f"{aqui}: el operador {operador!r} necesita al menos un valor")
        if operador in OPERADORES_ENTEROS:
            v = valores[0]
            if not isinstance(v, int) or isinstance(v, bool) or v < 0:
                raise ErrorPlaybook(f"{aqui}: el operador {operador!r} lleva un entero >= 0 (días) y trae {v!r}")


def _validar_filtros_anidados(filtros, donde, profundidad=1):
    """Grupos `{tipo, condiciones, filtros}` dentro de un filtro o de un enlace: el `filter or` anidado de la vista de riesgo."""
    if profundidad > PROFUNDIDAD_MAXIMA_DE_FILTROS:
        raise ErrorPlaybook(f"{donde}: los filtros anidan más de {PROFUNDIDAD_MAXIMA_DE_FILTROS} niveles; "
                            "eso no es una vista de grilla")
    for i, g in enumerate(filtros):
        aqui = f"{donde}.filtros[{i}]"
        exigir_forma(g, dict, aqui, "filtro")
        sobran = sorted(set(g) - {"tipo", "condiciones", "filtros"})
        if sobran:
            raise ErrorPlaybook(f"{aqui}: se admite {{tipo, condiciones, filtros}}; sobra {sobran}")
        if g.get("tipo", "and") not in ("and", "or"):
            raise ErrorPlaybook(f"{aqui}: tipo = {g.get('tipo')!r}; solo 'and' u 'or'")
        _validar_condiciones(exigir_forma(g.get("condiciones", []), list, aqui, "condiciones"), aqui)
        _validar_filtros_anidados(exigir_forma(g.get("filtros", []), list, aqui, "filtros"), aqui, profundidad + 1)


def _validar_enlaces(enlaces, donde, profundidad=0):
    if profundidad > 3:
        # Un fetch con más de tres saltos es señal de que la vista quiere algo
        # que no es una vista. Una vista de conversaciones por el cliente de su arreglo usa dos (Conversación → Arreglo → Cliente).
        raise ErrorPlaybook(f"{donde}: los enlaces anidan más de 3 niveles; eso no es una vista de grilla")
    for i, e in enumerate(enlaces):
        aqui = f"{donde}.enlaces[{i}]"
        exigir_forma(e, dict, aqui, "enlace")
        tipo = exigir_forma(e.get("tipo", "inner"), str, aqui, "tipo", no_vacio=True)
        if tipo not in TIPOS_ENLACE:
            raise ErrorPlaybook(f"{aqui}: tipo de enlace {tipo!r} no admitido; los válidos son {list(TIPOS_ENLACE)}")
        de_filtro = tipo in TIPOS_ENLACE_DE_FILTRO
        # `any` / `not any` no devuelven columnas: no tienen alias (nada que nombrar) y por eso tampoco enlaces adentro.
        obligatorias = {"tabla", "de", "a"} | (set() if de_filtro else {"alias"})
        faltan = sorted(obligatorias - set(e))
        sobran = sorted(set(e) - {"tabla", "tipo", "de", "a", "alias", "condiciones", "filtros", "enlaces"})
        if faltan or sobran:
            raise ErrorPlaybook(f"{aqui}: faltan {faltan}, sobran {sobran}")
        if de_filtro and "alias" in e:
            raise ErrorPlaybook(f"{aqui}: un enlace {tipo!r} no lleva alias (no devuelve columnas)")
        if de_filtro and e.get("enlaces"):
            raise ErrorPlaybook(f"{aqui}: un enlace {tipo!r} no admite enlaces adentro")
        for campo in obligatorias:
            exigir_forma(e[campo], str, aqui, campo, no_vacio=True)
        _validar_condiciones(exigir_forma(e.get("condiciones", []), list, aqui, "condiciones"), aqui)
        _validar_filtros_anidados(exigir_forma(e.get("filtros", []), list, aqui, "filtros"), aqui)
        _validar_enlaces(exigir_forma(e.get("enlaces", []), list, aqui, "enlaces"), aqui, profundidad + 1)


def _validar_estructura(datos):
    faltan, sobran = sorted(set(CLAVES) - set(datos)), sorted(set(datos) - set(CLAVES))
    if faltan or sobran:
        partes = []
        if faltan:
            partes.append(f"faltan {faltan}")
        if sobran:
            partes.append(f"sobran {sobran}")
        raise ErrorPlaybook(f"el bloque de la vista no tiene las claves esperadas: {'; '.join(partes)}")
    for clave, tipo in CLAVES.items():
        exigir_forma(datos[clave], tipo, "el bloque de la vista", clave, no_vacio=(tipo is str))
    if datos["tipo"] != TIPO:
        raise ErrorPlaybook(f"el bloque de la vista dice tipo = {datos['tipo']!r}, se esperaba {TIPO!r}")

    exigir_sin_tildes(datos["nombre"], "nombre")
    if len(datos["nombre"]) > LARGO_NOMBRE:
        raise ErrorPlaybook(f"nombre tiene {len(datos['nombre'])} caracteres; el máximo es {LARGO_NOMBRE}")
    if datos["clase"] not in CLASES:
        raise ErrorPlaybook(f"clase = {datos['clase']!r} no es válida; las válidas son {sorted(CLASES)}")

    columnas = exigir_forma(datos["columnas"], list, "el bloque de la vista", "columnas", no_vacio=True)
    nombres = []
    for i, c in enumerate(columnas):
        aqui = f"la columna [{i}]"
        exigir_forma(c, dict, aqui, "columna")
        sobran = sorted(set(c) - {"nombre", "ancho", "enlace"})
        if "nombre" not in c or sobran:
            raise ErrorPlaybook(f"{aqui}: se admite {{nombre, ancho, enlace}}; falta 'nombre' o sobra {sobran}")
        nombre = exigir_forma(c["nombre"], str, aqui, "nombre", no_vacio=True)
        if "enlace" in c:
            exigir_forma(c["enlace"], str, aqui, "enlace", no_vacio=True)
            nombre = c["enlace"] + "." + nombre  # la huella para detectar repetidas
        if "ancho" in c:
            ancho = exigir_forma(c["ancho"], int, aqui, "ancho")
            if not 25 <= ancho <= 800:
                raise ErrorPlaybook(f"{aqui}: ancho {ancho} fuera de 25..800")
        nombres.append(nombre)
    if len(set(nombres)) != len(nombres):
        raise ErrorPlaybook(f"hay columnas repetidas en la vista: {sorted({n for n in nombres if nombres.count(n) > 1})}")

    for i, o in enumerate(exigir_forma(datos["orden"], list, "el bloque de la vista", "orden")):
        aqui = f"el orden [{i}]"
        exigir_forma(o, dict, aqui, "orden")
        if sorted(o) != ["columna", "sentido"]:
            raise ErrorPlaybook(f"{aqui}: se admite exactamente {{columna, sentido}}")
        exigir_forma(o["columna"], str, aqui, "columna", no_vacio=True)
        if o["sentido"] not in ("asc", "desc"):
            raise ErrorPlaybook(f"{aqui}: sentido = {o['sentido']!r}; solo 'asc' o 'desc'")
        if o["columna"] not in nombres:
            # Ordenar por una columna que no se muestra deja al usuario sin
            # entender por qué la grilla está en ese orden.
            raise ErrorPlaybook(f"{aqui}: se ordena por '{o['columna']}', que la vista no muestra")

    filtro = datos["filtro"]
    sobran = sorted(set(filtro) - {"tipo", "condiciones", "filtros", "enlaces", "buscar"})
    if sobran:
        raise ErrorPlaybook(f"el filtro solo admite {{tipo, condiciones, filtros, enlaces, buscar}}; sobra {sobran}")
    if filtro.get("tipo", "and") not in ("and", "or"):
        raise ErrorPlaybook(f"el filtro tiene tipo = {filtro.get('tipo')!r}; solo 'and' u 'or'")
    _validar_condiciones(exigir_forma(filtro.get("condiciones", []), list, "el filtro", "condiciones"), "el filtro")
    _validar_filtros_anidados(exigir_forma(filtro.get("filtros", []), list, "el filtro", "filtros"), "el filtro")
    _validar_enlaces(exigir_forma(filtro.get("enlaces", []), list, "el filtro", "enlaces"), "el filtro")
    _validar_busqueda(datos["clase"], filtro)

    # Una columna que viene de una tabla enlazada tiene que nombrar un alias que
    # exista: si no, la celda queda vacia y eso no falla al crear la vista.
    alias_declarados = _alias_de(filtro.get("enlaces", []))
    for c in columnas:
        if "enlace" in c and c["enlace"] not in alias_declarados:
            raise ErrorPlaybook(
                f"la columna '{c['nombre']}' viene del enlace '{c['enlace']}', que el filtro no declara; "
                f"los alias declarados son {sorted(alias_declarados) or 'ninguno'}")


def _validar_busqueda(clase, filtro):
    """Una vista de búsqueda rápida (`querytype 4`) busca en las columnas de su filtro marcado `isquickfindfields="1"`
    (Learn, "Quick find"; probado en Dev, spike 2b): un `or` de `like` con `{0}`. Sin esa marca la búsqueda no encuentra nada y no
    falla al crearla; por eso la marca es obligatoria en esa clase y prohibida en las demás."""
    if "buscar" in filtro:
        exigir_forma(filtro["buscar"], bool, "el filtro", "buscar")
    buscar = filtro.get("buscar", False)
    if clase != "BusquedaRapida":
        if buscar:
            raise ErrorPlaybook(f"el filtro declara buscar = true, que solo vale en una vista de clase 'BusquedaRapida' (esta es {clase!r})")
        return
    if not buscar:
        raise ErrorPlaybook("una vista 'BusquedaRapida' tiene que declarar buscar = true en el filtro "
                            "(es lo que marca las columnas por las que se busca)")
    if filtro.get("tipo", "and") != "or":
        raise ErrorPlaybook("el filtro de una búsqueda rápida lleva tipo 'or': basta que el texto esté en una columna")
    if filtro.get("enlaces") or filtro.get("filtros"):
        raise ErrorPlaybook("el filtro de una búsqueda rápida no admite ni enlaces ni filtros anidados: solo condiciones")
    condiciones = filtro.get("condiciones", [])
    if not condiciones:
        raise ErrorPlaybook("el filtro de una búsqueda rápida necesita al menos una condición (una columna por la que buscar)")
    for i, c in enumerate(condiciones):
        if c["operador"] != "like":
            raise ErrorPlaybook(f"el filtro.condiciones[{i}] de una búsqueda rápida usa el operador {c['operador']!r}; solo 'like'")
        if c["valores"] != [VALOR_DE_BUSQUEDA]:
            raise ErrorPlaybook(
                f"el filtro.condiciones[{i}] de una búsqueda rápida lleva el valor {c['valores']!r}; tiene que ser ['{VALOR_DE_BUSQUEDA}']")


def _alias_de(enlaces):
    alias = set()
    for e in enlaces:
        if "alias" in e:
            alias.add(e["alias"])
        alias |= _alias_de(e.get("enlaces", []))
    return alias


def validar_playbook(datos):
    _validar_estructura(datos)


# ---------------------------------------------------------------------------
# Generación del XML
# ---------------------------------------------------------------------------
def _xml_condiciones(condiciones, sangria):
    partes = []
    for c in condiciones:
        attrs = f"attribute={quoteattr(c['columna'])} operator={quoteattr(c['operador'])}"
        valores = c.get("valores", [])
        if c["operador"] in OPERADORES_VARIOS_VALORES:
            internos = "".join(f"<value>{escape(str(v))}</value>" for v in valores)
            partes.append(f"{sangria}<condition {attrs}>{internos}</condition>")
        elif valores:
            partes.append(f"{sangria}<condition {attrs} value={quoteattr(str(valores[0]))} />")
        else:
            partes.append(f"{sangria}<condition {attrs} />")
    return partes


def _de_filtro(e):
    """`any` / `not any`: Learn los documenta DENTRO de un `<filter>` (siguen su `type`); los demás son hijos del padre."""
    return e.get("tipo", "inner") in TIPOS_ENLACE_DE_FILTRO


def _xml_filtro(tipo, condiciones, filtros, sangria, extra="", enlaces_de_filtro=()):
    """Un `<filter>` con sus condiciones, sus filtros anidados y, al final, los enlaces `any` / `not any`."""
    partes = [f"{sangria}<filter type={quoteattr(tipo)}{extra}>"]
    partes.extend(_xml_condiciones(condiciones, sangria + "  "))
    for g in filtros:
        partes.extend(_xml_filtro(g.get("tipo", "and"), g.get("condiciones", []), g.get("filtros", []), sangria + "  "))
    for e in enlaces_de_filtro:
        partes.extend(_xml_enlace(e, sangria + "  ", {}))
    partes.append(f"{sangria}</filter>")
    return partes


def _xml_enlace(e, sangria, atributos_por_alias):
    tipo = e.get("tipo", "inner")
    alias = f" alias={quoteattr(e['alias'])}" if "alias" in e else ""
    partes = [f"{sangria}<link-entity name={quoteattr(e['tabla'])} from={quoteattr(e['de'])} "
              f"to={quoteattr(e['a'])}{alias} link-type={quoteattr(tipo)}>"]
    for atributo in atributos_por_alias.get(e.get("alias"), []):
        partes.append(f"{sangria}  <attribute name={quoteattr(atributo)} />")
    condiciones, filtros = e.get("condiciones", []), e.get("filtros", [])
    hijos = e.get("enlaces", [])
    en_filtro = [h for h in hijos if _de_filtro(h)]
    if condiciones or filtros or en_filtro:
        partes.extend(_xml_filtro("and", condiciones, filtros, sangria + "  ", enlaces_de_filtro=en_filtro))
    partes.extend(_xml_enlaces(hijos, sangria + "  ", atributos_por_alias))
    partes.append(f"{sangria}</link-entity>")
    return partes


def _xml_enlaces(enlaces, sangria, atributos_por_alias=None):
    """Los enlaces que son hijos directos de su padre (los `inner`); los `any` / `not any` los emite el filtro del padre."""
    atributos_por_alias = atributos_por_alias or {}
    partes = []
    for e in enlaces:
        if not _de_filtro(e):
            partes.extend(_xml_enlace(e, sangria, atributos_por_alias))
    return partes


def fetchxml(datos, primaria):
    """El `fetchxml` de la vista. La columna primaria va siempre: la grilla la
    necesita para abrir el registro."""
    propias = [c["nombre"] for c in datos["columnas"] if "enlace" not in c]
    atributos = [primaria] + [c for c in propias if c != primaria]
    por_alias = {}
    for c in datos["columnas"]:
        if "enlace" in c:
            por_alias.setdefault(c["enlace"], []).append(c["nombre"])
    partes = ['<fetch version="1.0" output-format="xml-platform" mapping="logical">',
              f'  <entity name={quoteattr(datos["tabla"])}>']
    partes += [f'    <attribute name={quoteattr(a)} />' for a in atributos]
    for o in datos["orden"]:
        partes.append(f'    <order attribute={quoteattr(o["columna"])} descending="{str(o["sentido"] == "desc").lower()}" />')
    filtro = datos["filtro"]
    condiciones, filtros = filtro.get("condiciones", []), filtro.get("filtros", [])
    en_filtro = [e for e in filtro.get("enlaces", []) if _de_filtro(e)]
    if condiciones or filtros or en_filtro:
        marca = ' isquickfindfields="1"' if filtro.get("buscar") else ""
        partes.extend(_xml_filtro(filtro.get("tipo", "and"), condiciones, filtros, "    ", marca, en_filtro))
    partes.extend(_xml_enlaces(filtro.get("enlaces", []), "    ", por_alias))
    partes += ["  </entity>", "</fetch>"]
    return "\n".join(partes)


def layoutxml(datos, primaria, codigo_tabla):
    """El `layoutxml`. Cada `<cell>` tiene que tener su `<attribute>` en el
    fetch: por eso los dos se generan del mismo playbook y nunca a mano."""
    def celda(c):
        return c["enlace"] + "." + c["nombre"] if "enlace" in c else c["nombre"]

    partes = [f'<grid name="resultset" object="{codigo_tabla}" jump={quoteattr(celda(datos["columnas"][0]))} '
              'select="1" preview="1" icon="1">',
              f'  <row name="result" id={quoteattr(primaria)}>']
    for c in datos["columnas"]:
        partes.append(f'    <cell name={quoteattr(celda(c))} width="{c.get("ancho", ANCHO_POR_DEFECTO)}" />')
    partes += ["  </row>", "</grid>"]
    return "\n".join(partes)


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
def _definicion_de_tabla(dv, logical_name):
    ruta = f"EntityDefinitions(LogicalName='{logical_name}')?$select=PrimaryIdAttribute,ObjectTypeCode,MetadataId"
    cuerpo = leer_entorno(dv, ruta, C_TABLA, admite_404=True)
    if cuerpo is None:
        raise Bloqueado(f"la tabla '{logical_name}' no existe en el entorno")
    return (exigir_forma(cuerpo.get("PrimaryIdAttribute"), str, C_TABLA, "PrimaryIdAttribute", no_vacio=True),
            exigir_forma(cuerpo.get("ObjectTypeCode"), int, C_TABLA, "ObjectTypeCode"),
            exigir_forma(cuerpo.get("MetadataId"), str, C_TABLA, "MetadataId", no_vacio=True))


def _leer_vista(dv, nombre, tabla):
    ruta = ("savedqueries?$select=savedqueryid,name,returnedtypecode,querytype,isdefault,description,"
            "fetchxml,layoutxml,statecode,ismanaged,isquickfindquery"
            f"&$filter=name eq '{nombre}' and returnedtypecode eq '{tabla}'")
    cuerpo = leer_entorno(dv, ruta, C_VISTA)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_VISTA, "value")
    if len(filas) > 1:
        raise ErrorEntorno(
            f"{C_VISTA} devolvió {len(filas)} vistas llamadas '{nombre}' sobre '{tabla}'; el nombre es la clave de negocio")
    return filas[0] if filas else None


def _busquedas_rapidas(dv, tabla):
    """Las vistas de búsqueda rápida (`querytype 4`) de la tabla. Toda tabla nace con una (spike 2b) y solo cuenta esa:
    una segunda no se usa. Por eso la herramienta nunca CREA una; repara la que hay con `--completar`."""
    ruta = ("savedqueries?$select=savedqueryid,name"
            f"&$filter=returnedtypecode eq '{tabla}' and querytype eq {CLASES['BusquedaRapida']}")
    cuerpo = leer_entorno(dv, ruta, C_BUSQUEDAS)
    return exigir_forma(cuerpo.get("value"), [dict], C_BUSQUEDAS, "value")


def _normalizar_xml(texto):
    """Compara XML sin que cuenten como diferencia ni los espacios ni el
    `savedqueryid` que la plataforma INYECTA en el `fetchxml` al guardarlo
    (verificado en Dev el 2026-09-22): ese atributo no lo escribimos nosotros y
    no puede estar en el XML generado, así que compararlo daría "difiere"
    siempre."""
    apretado = "".join((texto or "").split())
    while True:
        i = apretado.find('savedqueryid="')
        if i < 0:
            return apretado
        fin = apretado.find('"', i + len('savedqueryid="'))
        if fin < 0:
            return apretado
        apretado = apretado[:i] + apretado[fin + 1:]


def _diferencias(datos, vista, fetch, layout):
    difs, corregibles = [], {}
    if vista.get("querytype") != CLASES[datos["clase"]]:
        difs.append(f"querytype es {vista.get('querytype')!r} y el playbook dice {datos['clase']} "
                    "(INMUTABLE en la práctica: conviene borrar la vista y rehacerla)")
    for campo, valor in (("name", datos["nombre"]), ("description", datos["descripcion"]),
                         ("isdefault", datos["pordefecto"])):
        if vista.get(campo) != valor:
            difs.append(f"{campo} es {vista.get(campo)!r} y el playbook dice {valor!r}")
            corregibles[campo] = valor
    for campo, generado in (("fetchxml", fetch), ("layoutxml", layout)):
        if _normalizar_xml(vista.get(campo)) != _normalizar_xml(generado):
            difs.append(f"{campo} no coincide con el que genera el playbook")
            corregibles[campo] = generado
    if vista.get("ismanaged"):
        difs.append("la vista está managed en este entorno; no se toca desde acá")
        corregibles.clear()
    return difs, corregibles


COMPONENTE_TABLA = 1
COMPONENTE_VISTA = 26
INCLUYE_SUBCOMPONENTES = 0


def _vista_en_la_solucion(dv, solution_id, id_vista):
    """La fila PROPIA de la vista (`componenttype 26`, `objectid` = savedqueryid). Existe cuando la tabla está en la
    solución sin incluir subcomponentes (p. ej. la estándar `email`, rootcomponentbehavior 1): ahí la vista es un
    componente suelto y esta fila es la única prueba de que viaja. Con la tabla en 0 no hay fila propia (la vista
    viaja dentro de la tabla)."""
    ruta = ("solutioncomponents?$select=componenttype,rootcomponentbehavior,objectid"
            f"&$filter=_solutionid_value eq {solution_id} and objectid eq {id_vista} "
            f"and componenttype eq {COMPONENTE_VISTA}")
    cuerpo = leer_entorno(dv, ruta, C_COMPONENTES)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_COMPONENTES, "value")
    return filas[0] if filas else None


def _tabla_en_la_solucion(dv, solution_id, metadata_id):
    """Con la tabla en la solución con `rootcomponentbehavior = 0` la vista **no
    tiene fila propia** en `solutioncomponents`: viaja dentro de su tabla
    (verificado en Dev el 2026-09-22). Es el respaldo cuando `_vista_en_la_solucion`
    no encuentra fila propia; con otro comportamiento la vista no viajaría."""
    ruta = ("solutioncomponents?$select=componenttype,rootcomponentbehavior,objectid"
            f"&$filter=_solutionid_value eq {solution_id} and objectid eq {metadata_id} "
            f"and componenttype eq {COMPONENTE_TABLA}")
    cuerpo = leer_entorno(dv, ruta, C_COMPONENTES)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_COMPONENTES, "value")
    return filas[0] if filas else None


def _otras_por_defecto(dv, tabla, id_vista):
    """Las OTRAS vistas publicas de la misma tabla marcadas por defecto.

    Dataverse NO desmarca sola la anterior al crear una nueva con
    `isdefault = true`: quedan dos, y con dos la plataforma elige una y la
    declaracion del playbook pasa a ser mentira. Toda tabla nace con una
    "Active X" de fabrica marcada por defecto, asi que esto pasa SIEMPRE
    (verificado en Dev el 2026-09-22, 8 tablas con dos defaults)."""
    ruta = ("savedqueries?$select=savedqueryid,name"
            f"&$filter=returnedtypecode eq '{tabla}' and querytype eq {CLASES['Publica']} and isdefault eq true")
    cuerpo = leer_entorno(dv, ruta, C_VISTA)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_VISTA, "value")
    return [f for f in filas if f.get("savedqueryid") != id_vista]


def _publicar(dv, tabla, dormir):
    """Learn: una personalización se publica sola al CREARSE; hay que publicar
    después de ACTUALIZARLA. Se publica la tabla entera, que es el nodo que
    Learn documenta para vistas."""
    cuerpo = {"ParameterXml": f"<importexportxml><entities><entity>{tabla}</entity></entities></importexportxml>"}
    return escribir_metadatos(dv, "POST", "PublishXml", cuerpo, dormir=dormir)


def _contra_entorno(dv, datos, identidad, solo_verificar, completar, componente, dormir):
    solucion = identidad["solucion"]
    solution_id = comprobar_solucion_e_idioma(dv, identidad)
    primaria, codigo_tabla, metadata_id = _definicion_de_tabla(dv, datos["tabla"])
    fetch = fetchxml(datos, primaria)
    layout = layoutxml(datos, primaria, codigo_tabla)

    vista = _leer_vista(dv, datos["nombre"], datos["tabla"])
    hubo_escritura = False
    hubo_alta = False

    if vista is None and datos["clase"] == "BusquedaRapida":
        # No se crea: se bloquea con los nombres reales (o con "ninguna") para que el playbook tome el de Dev.
        hay = [str(f.get("name")) for f in _busquedas_rapidas(dv, datos["tabla"])]
        raise Bloqueado(
            f"la vista de búsqueda rápida '{datos['nombre']}' no existe sobre '{datos['tabla']}' y no se crea: "
            + (f"la tabla tiene {', '.join(repr(n) for n in hay)}; el playbook tiene que llevar ese nombre y repararla con --completar"
               if hay else "la tabla no tiene ninguna, y toda tabla nace con una; revisá el entorno"))

    if vista is None:
        if solo_verificar:
            return "difiere", componente, f"la vista '{datos['nombre']}' no existe sobre '{datos['tabla']}'"
        cuerpo = {
            "name": datos["nombre"],
            "description": datos["descripcion"],
            "returnedtypecode": datos["tabla"],
            "querytype": CLASES[datos["clase"]],
            "isdefault": datos["pordefecto"],
            "fetchxml": fetch,
            "layoutxml": layout,
        }
        est, resp, _ = escribir_metadatos(dv, "POST", "savedqueries", cuerpo, dormir=dormir, solucion=solucion)
        if est not in (200, 201, 204):
            return "error", componente, f"el alta devolvió HTTP {est} (se esperaba 204): {resp}"
        vista = _leer_vista(dv, datos["nombre"], datos["tabla"])
        if vista is None:
            return "error", componente, "el alta no dio error pero la vista no aparece al releer"
        hubo_escritura = True
        hubo_alta = True
    elif completar and not solo_verificar:
        difs, corregibles = _diferencias(datos, vista, fetch, layout)
        if not [d for d in difs if "INMUTABLE" in d] and corregibles:
            id_vista = exigir_forma(vista.get("savedqueryid"), str, C_VISTA, "savedqueryid", no_vacio=True)
            if datos["clase"] == "BusquedaRapida":
                # La plataforma rechaza (400 "An unexpected error occurred") un PATCH de una búsqueda rápida que no
                # lleve estos cuatro campos juntos (spikes/plataforma/RESULTADO.md).
                corregibles = dict(corregibles, fetchxml=fetch, layoutxml=layout, name=datos["nombre"],
                                   returnedtypecode=datos["tabla"])
            est, resp, _ = escribir_metadatos(
                dv, "PATCH", f"savedqueries({id_vista})", corregibles, dormir=dormir, solucion=solucion)
            if est not in (200, 204):
                return "error", componente, f"la corrección devolvió HTTP {est} (se esperaba 204): {resp}"
            est, resp, _ = _publicar(dv, datos["tabla"], dormir)
            if est not in (200, 204):
                return "error", componente, f"la vista se corrigió pero la publicación devolvió HTTP {est}: {resp}"
            vista = _leer_vista(dv, datos["nombre"], datos["tabla"])
            hubo_escritura = True

    id_vista = exigir_forma(vista.get("savedqueryid"), str, C_VISTA, "savedqueryid", no_vacio=True)

    # Una vista por defecto tiene que ser LA unica por defecto. Esto se corrige
    # siempre que se este escribiendo, sin pedir `--completar`: no es un ajuste
    # de gusto, es que la declaracion del playbook sea cierta o no lo sea.
    if datos["pordefecto"] and datos["clase"] == "Publica":
        otras = _otras_por_defecto(dv, datos["tabla"], id_vista)
        if otras and not solo_verificar:
            for otra in otras:
                est, resp, _ = escribir_metadatos(
                    dv, "PATCH", f"savedqueries({otra['savedqueryid']})", {"isdefault": False},
                    dormir=dormir, solucion=solucion)
                if est not in (200, 204):
                    return "error", componente, (
                        f"no se pudo desmarcar por defecto la vista '{otra.get('name')}': HTTP {est} {resp}")
            est, resp, _ = _publicar(dv, datos["tabla"], dormir)
            if est not in (200, 204):
                return "error", componente, f"se desmarcaron las otras por defecto pero la publicación devolvió HTTP {est}: {resp}"
            hubo_escritura = True
            otras = _otras_por_defecto(dv, datos["tabla"], id_vista)
        if otras:
            difs_extra = [f"'{o.get('name')}'" for o in otras]
            return ("error" if hubo_escritura else "difiere"), componente, (
                f"la vista se declara por defecto pero tambien lo estan {', '.join(difs_extra)}: "
                "con dos por defecto la plataforma elige una sola y la declaracion no se cumple")

    difs, _ = _diferencias(datos, vista, fetch, layout)
    if datos["clase"] == "BusquedaRapida":
        if not exigir_forma(vista.get("isquickfindquery"), bool, C_VISTA, "isquickfindquery"):
            difs.append("isquickfindquery es False: la plataforma no la trata como la vista de búsqueda rápida de la tabla")
        sobrantes = [str(o.get("name")) for o in _busquedas_rapidas(dv, datos["tabla"])
                     if o.get("savedqueryid") != id_vista]
        if sobrantes:
            difs.append(f"la tabla tiene otras vistas de búsqueda rápida ({', '.join(repr(n) for n in sobrantes)}); "
                        "solo cuenta una y no se borran desde acá")
    propia = _vista_en_la_solucion(dv, solution_id, id_vista)
    fila = None
    if propia is None:
        fila = _tabla_en_la_solucion(dv, solution_id, metadata_id)
        if fila is None:
            difs.append(f"ni la vista ni la tabla '{datos['tabla']}' figuran en la solución (solutionid {solution_id}), "
                        "así que la vista no viajaría")
        elif fila.get("rootcomponentbehavior") != INCLUYE_SUBCOMPONENTES:
            difs.append(f"la tabla '{datos['tabla']}' está en la solución con rootcomponentbehavior "
                        f"{fila.get('rootcomponentbehavior')!r} y no {INCLUYE_SUBCOMPONENTES} (incluir subcomponentes), "
                        "y la vista no tiene fila propia en la solución: no viajaría")

    if difs:
        estado = "error" if hubo_escritura else "difiere"
        prefijo = "se escribió pero no quedó bien: " if hubo_escritura else ""
        return estado, componente, prefijo + "; ".join(difs)

    return estado_de_escritura(hubo_alta, hubo_escritura), componente, (
        f"savedqueryid {id_vista}, {datos['clase']} sobre {datos['tabla']}, {len(datos['columnas'])} columnas"
        f"{', por defecto' if datos['pordefecto'] else ''}; "
        + (f"figura en la solución '{solucion}' con fila propia (componenttype {COMPONENTE_VISTA})" if propia is not None
           else f"viaja dentro de la tabla, que está en la solución '{solucion}' "
                f"con rootcomponentbehavior {fila.get('rootcomponentbehavior')}"))


def construir(ruta_playbook, solo_verificar, fabrica_cliente, completar=False, dormir=None):
    """Nunca lanza: siempre devuelve `(estado, componente, detalle)`."""
    if dormir is None:
        import time

        dormir = time.sleep
    componente = os.path.basename(ruta_playbook)
    paso = "leer el playbook"
    try:
        secciones = dividir_secciones(leer_texto(ruta_playbook))
        paso = "leer el bloque '## 2. Qué se crea'"
        datos = obtener_componente(secciones, TIPO)
        if isinstance(datos.get("nombre"), str):
            componente = datos["nombre"]
        paso = "leer el bloque '## 1. Identidad'"
        identidad = obtener_identidad(secciones)
        paso = "validar el bloque de la vista"
        validar_playbook(datos)
    except ErrorPlaybook as e:
        return "error", componente, str(e)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except OSError as e:
        return "error", componente, f"no se pudo leer el playbook '{ruta_playbook}': {e}"
    except Exception as e:
        return "error", componente, f"fallo inesperado validando el playbook (offline), al {paso}: {type(e).__name__}: {e}"

    print(f"Playbook leído y validado (offline). tipo={TIPO} nombre={componente}")
    try:
        dv = fabrica_cliente()
    except Exception:
        return "error", componente, MENSAJE_FALLO_CLIENTE

    rastro = Rastro(dv)
    try:
        return _contra_entorno(rastro, datos, identidad, solo_verificar, completar, componente, dormir)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except ErrorEntorno as e:
        return "error", componente, str(e)
    except Exception as e:
        return "error", componente, f"fallo inesperado hablando con Dataverse, durante {rastro.en_curso}: {type(e).__name__}: {e}"


def main():
    argv = sys.argv[1:]
    rutas = [a for a in argv if not a.startswith("--")]
    banderas = {a for a in argv if a.startswith("--")}
    desconocidas = sorted(banderas - {"--solo-verificar", "--completar"})
    if len(rutas) != 1 or desconocidas:
        return salida(
            "error", "desconocido",
            "uso incorrecto: vista.py <playbook.md> [--solo-verificar] [--completar]"
            + (f"; banderas desconocidas: {desconocidas}" if desconocidas else ""),
        )
    from dataverse_api import Dataverse

    estado, componente, detalle = construir(
        rutas[0], "--solo-verificar" in banderas, Dataverse, completar="--completar" in banderas)
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
