#!/usr/bin/env python3
"""Construye EL formulario de una tabla desde un playbook de tipo `formulario`:
el principal (`systemform` de tipo Main) o el de creación rápida (tipo Quick
Create), según la clave opcional `clase`. Contrato: `power-platform-construir`.

    python3 herramientas/construir/formulario.py <playbook.md> [--solo-verificar] [--completar]

Tres decisiones que valen la pena:

1. **ACTUALIZA el formulario principal que ya existe, no crea uno nuevo.** Cada
   tabla nace con un Main llamado "Information". Crear un segundo Main deja dos
   formularios compitiendo, que es el mismo problema que ya nos mordió con las
   vistas por defecto. Se busca el Main de la tabla y se le cambia el nombre y
   el `formxml`.

2. **El playbook no lleva XML, ni `classid`, ni etiquetas.** Declara secciones y
   columnas; la herramienta resuelve el tipo de cada columna contra la metadata
   y de ahí saca su control, y toma la etiqueta del `DisplayName` de la columna.
   Una etiqueta escrita a mano en el playbook se despega de la tabla el día que
   alguien renombra la columna.

3. **Los GUID del XML son deterministas** (derivados del nombre del formulario
   y de la posición del elemento). Si fueran aleatorios, cada corrida generaría
   un XML distinto, la herramienta diría "difiere" para siempre y nunca
   convergería.

**Idiomas.** Toda etiqueta embebida en el `formxml` (pestaña, secciones, campos, relleno del encabezado) sale UNA
VEZ POR CADA idioma de la Identidad, en ese orden: ahí la plataforma no cae al idioma base. Contrato del playbook:
`pestana` y el `titulo` de cada sección son un objeto `{"1033": "...", "3082": "..."}`, un texto por idioma, sin
tildes (BP-PP-197); la etiqueta de cada campo sale del `DisplayName` de la columna, un texto por idioma, y si la
columna no tiene alguno queda `bloqueado`. La verificación compara cada idioma en los dos sentidos.

**Formato ampliado (diseno/07).** `pestanas: [{titulo, secciones}]` reemplaza a `pestana` + `secciones` (una forma o la otra; el XML
de una sola pestaña no cambia). Un campo (de `campos` o de `encabezado`) es `"col"` o `{"nombre": "col", "solo_lectura": bool}`, y el
del campo manda sobre el `solo_lectura` del formulario. Una sección `{titulo, timeline: {alto}}` pone el control Timeline
(`notescontrol`, sin parámetros; qué actividades muestra no está en el XML). `subgrilla` admite `registros_por_pagina` (2 a 250). `bibliotecas`
(web resources JS que tienen que existir) y `eventos: [{evento: onload|onsave|onchange, biblioteca, funcion, columna?
(solo onchange), pasar_contexto?}]` generan `<formLibraries>` y `<events>`.

Los `classid` NO son de memoria: se derivaron leyendo formularios reales del
entorno y cruzándolos contra el tipo de cada columna. La plataforma **no
normaliza** el `classid` (se probó con una sonda): guarda el que se le manda,
así que uno equivocado no falla al guardar y se ve roto recién en la app.

Última línea de la salida: JSON de una línea con `estado`
(creado | actualizado | ya_existia | difiere | bloqueado | error), `componente`, `detalle`.
"""
import os
import re
import sys
import uuid
from xml.etree import ElementTree as ET
from xml.sax.saxutils import quoteattr

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
    exigir_etiqueta,
    exigir_forma,
    exigir_sin_tildes,
    leer_entorno,
    leer_texto,
    obtener_componente,
    obtener_identidad,
    salida,
    textos_del_entorno,
    validar_textos_por_idioma,
)

TIPO = "formulario"
FORMULARIO_PRINCIPAL = 2  # Learn, systemform.type: 2 = Main
# Learn, systemform.type: 7 = Quick Create. Es el diálogo chico que abre
# `Xrm.Navigation.openForm({useQuickCreateForm: true})`: sin barra de comandos, sin
# pestañas y sin encabezado de registro — solo los campos y "Guardar y cerrar".
# La tabla tiene que tener `IsQuickCreateEnabled` en `true` o el formulario no se muestra.
FORMULARIO_CREACION_RAPIDA = 7
ACTIVO = 1

# Derivados empíricamente de formularios reales del entorno (2026-09-22), no de
# memoria: se leyeron los Main de account/contact/systemuser/task y se cruzó
# cada `classid` contra el `AttributeType` de su columna. El de File lo enseñó
# un formulario que armó el aprobador a mano, porque ninguna tabla del entorno
# tenía una columna de archivo en un formulario.
# La clave es `AttributeTypeName`, NO `AttributeType`: una columna de ARCHIVO
# reporta `AttributeType = "Virtual"`, igual que las columnas `*name` que
# acompañan a cada choice. Lo que las distingue es `AttributeTypeName`:
# `FileType` contra `VirtualType` (verificado en Dev el 2026-09-22).
CONTROL_POR_TIPO = {
    "StringType": "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}",
    "MemoType": "{E0DECE4B-6FC8-4a8f-A065-082708572369}",
    "PicklistType": "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}",
    "StateType": "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}",
    "StatusType": "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}",
    "BooleanType": "{67FAC785-CD58-4f9f-ABB3-4B7DDC6ED5ED}",
    "LookupType": "{270BD3DB-D9AF-4782-9025-509E298DEC0A}",
    "OwnerType": "{270BD3DB-D9AF-4782-9025-509E298DEC0A}",
    "CustomerType": "{270BD3DB-D9AF-4782-9025-509E298DEC0A}",
    "DateTimeType": "{5B773807-9FB2-42db-97C3-7A91EFF8ADFF}",
    "IntegerType": "{C6D124CA-7EDA-4a60-AEA9-7FB8D318B68F}",
    "MoneyType": "{533B9E00-756B-4312-95A0-DC888637AC78}",
    # Decimal: leído de un formulario real de Dev el 2026-10-20 (`feedback.normalizedrating`, AttributeTypeName DecimalType),
    # con el casing mixto con que lo escribe la plataforma.
    "DecimalType": "{C3EFE0C3-0EC6-42be-8349-CBD9079DFD8E}",
    "FileType": "{0A7FF475-B016-4687-9CE5-042BFDBD6519}",
}
CONTROL_SUBGRILLA = "{E7A81278-8635-4D9E-8D4D-59480B391C5B}"
# Learn: el máximo de filas de una subgrilla va de 2 a 250. Por omisión, 10 (lo que siempre se generó).
REGISTROS_MINIMOS, REGISTROS_MAXIMOS, REGISTROS_POR_OMISION = 2, 250, 10

# Espacio de nombres propio para los GUID deterministas del XML.
SEMILLA = uuid.UUID("7d1f6a52-0c34-4a8e-9f21-5e0a9c3b7d40")

LARGO_NOMBRE = 200

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)

CLAVES = {"tipo": str, "nombre": str, "tabla": str, "descripcion": str, "pestana": dict,
          "solo_lectura": bool, "encabezado": list, "secciones": list}
# Se validan aparte, con `validar_textos_por_idioma` (un texto por idioma de la Identidad).
CLAVES_DE_TEXTO_POR_IDIOMA = ("pestana",)

# Las dos formas del bloque: la de UNA pestaña (`pestana` + `secciones`, la original) o la de varias (`pestanas`, una lista de
# `{titulo, secciones}`). Son excluyentes: la otra clave sobra. Las demás son comunes.
CLAVES_COMUNES = {c: t for c, t in CLAVES.items() if c not in ("pestana", "secciones")}
CLAVES_DE_UNA_PESTANA = ("pestana", "secciones")

# OPCIONALES. `clase` por omisión vale "principal". `bibliotecas` (web resources JavaScript del formulario) y `eventos`
# (manejadores de OnLoad / OnSave / OnChange) por omisión no hay ninguno y el `formxml` no los trae.
CLAVES_OPCIONALES = {"clase": str, "pestanas": list, "bibliotecas": list, "eventos": list}

# Eventos de formulario que genera la herramienta, y las claves de cada manejador.
EVENTOS = ("onchange", "onload", "onsave")
CLAVES_DE_EVENTO = {"evento", "biblioteca", "funcion"}
CLAVES_OPCIONALES_DE_EVENTO = {"columna", "pasar_contexto"}
NOMBRE_DE_FUNCION = re.compile(r"^[A-Za-z_$][\w$]*(\.[A-Za-z_$][\w$]*)*$")
TIPO_JS = 3                  # webresourceset.webresourcetype: 3 = Script (JScript)

# Control Timeline: `notescontrol`, leído de los Main reales de `contact` en Dev el 2026-10-20. Sin parámetros.
CONTROL_TIMELINE = "{06375649-C143-495E-A496-C962E5B4488E}"
ID_DEL_TIMELINE = "notescontrol"
ALTO_MAXIMO_TIMELINE = 30
CLASES = {"principal": FORMULARIO_PRINCIPAL, "creacion_rapida": FORMULARIO_CREACION_RAPIDA}
CLASE_POR_DEFECTO = "principal"

C_FORM = "la consulta del formulario (GET systemforms)"
C_TABLA = "la consulta de la definición de la tabla (GET EntityDefinitions)"
C_COLUMNAS = "la consulta de las columnas de la tabla (GET EntityDefinitions/Attributes)"
# `classid` del control de web resource en un formulario. Sacado de formularios
# REALES del entorno (`mspp_webform`, `mspp_entityformmetadata`), no de memoria.
# Ojo con el casing mixto: la plataforma lo escribe así.
CONTROL_WEBRESOURCE = "{9FDF5F91-88B1-47f4-AD53-C11EFC01A01D}"
TIPO_HTML = 1            # webresourceset.webresourcetype: 1 = HTML

C_WEBRESOURCE = "la consulta del web resource del formulario (GET webresourceset)"

C_VISTA = "la consulta de la vista de la subgrilla (GET savedqueries)"
C_RELACION = "la consulta de la relación de la subgrilla (GET OneToManyRelationships)"
C_COMPONENTES = "la consulta de pertenencia a la solución (GET solutioncomponents)"

COMPONENTE_TABLA = 1
INCLUYE_SUBCOMPONENTES = 0


def guid(*partes):
    """GUID determinista: la misma entrada da siempre el mismo valor, así el
    XML generado es estable entre corridas."""
    return "{" + str(uuid.uuid5(SEMILLA, "|".join(str(p) for p in partes))).upper() + "}"


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def clase_de(datos):
    """El nombre de clase del playbook, con su valor por omisión."""
    return datos.get("clase", CLASE_POR_DEFECTO)


def tipo_de_formulario(datos):
    """El `systemform.type` que le corresponde. Es lo que separa un formulario
    principal de uno de creación rápida en TODAS las consultas y escrituras: leer el
    que no es hace que la herramienta crea que el formulario no existe y lo duplique."""
    return CLASES[clase_de(datos)]


def pestanas_de(datos):
    """Las pestañas del bloque como `[{"titulo": {...}, "secciones": [...]}]`, sea cual sea la forma en que se declararon.
    La forma de una pestaña (`pestana` + `secciones`) es una lista de una."""
    if "pestanas" in datos:
        return datos["pestanas"]
    return [{"titulo": datos["pestana"], "secciones": datos["secciones"]}]


def secciones_de(datos):
    """Todas las secciones de todas las pestañas, en orden: su posición es el índice global que usan los ids del XML
    y las subgrillas resueltas."""
    return [s for p in pestanas_de(datos) for s in p["secciones"]]


def nombre_de_campo(campo):
    """El nombre de la columna de una entrada de `campos` / `encabezado`: un texto o `{nombre, solo_lectura}`."""
    return campo["nombre"] if isinstance(campo, dict) else campo


def _solo_lectura_de(campo, del_formulario):
    """Un campo con `{nombre, solo_lectura}` manda sobre el `solo_lectura` del formulario, en los dos sentidos."""
    return campo["solo_lectura"] if isinstance(campo, dict) else del_formulario


def columnas_declaradas(datos):
    """Las columnas del formulario, en orden: encabezado y luego las secciones."""
    return ([nombre_de_campo(c) for c in datos["encabezado"]]
            + [nombre_de_campo(c) for s in secciones_de(datos) for c in s.get("campos", [])])


def _validar_campo(campo, donde, indice):
    aqui = f"{donde}[{indice}]"
    if isinstance(campo, dict):
        faltan, sobran = sorted({"nombre", "solo_lectura"} - set(campo)), sorted(set(campo) - {"nombre", "solo_lectura"})
        if faltan or sobran:
            raise ErrorPlaybook(f"{aqui}: se admite {{nombre, solo_lectura}}; faltan {faltan}, sobran {sobran}")
        exigir_forma(campo["nombre"], str, aqui, "nombre", no_vacio=True)
        exigir_forma(campo["solo_lectura"], bool, aqui, "solo_lectura")
    else:
        exigir_forma(campo, str, "el bloque del formulario", aqui, no_vacio=True)


def _validar_bibliotecas_y_eventos(datos, columnas):
    bibliotecas = exigir_forma(datos.get("bibliotecas", []), [str], "el bloque del formulario", "bibliotecas")
    for i, b in enumerate(bibliotecas):
        exigir_forma(b, str, "el bloque del formulario", f"bibliotecas[{i}]", no_vacio=True)
    if len(set(bibliotecas)) != len(bibliotecas):
        raise ErrorPlaybook(
            f"hay bibliotecas repetidas: {sorted({b for b in bibliotecas if bibliotecas.count(b) > 1})}")
    vistos = set()
    for i, e in enumerate(exigir_forma(datos.get("eventos", []), list, "el bloque del formulario", "eventos")):
        aqui = f"eventos[{i}]"
        exigir_forma(e, dict, "el bloque del formulario", aqui)
        faltan = sorted(CLAVES_DE_EVENTO - set(e))
        sobran = sorted(set(e) - CLAVES_DE_EVENTO - CLAVES_OPCIONALES_DE_EVENTO)
        if faltan or sobran:
            raise ErrorPlaybook(f"{aqui}: faltan {faltan}, sobran {sobran}")
        evento = exigir_forma(e["evento"], str, aqui, "evento", no_vacio=True)
        if evento not in EVENTOS:
            raise ErrorPlaybook(f"{aqui}: evento {evento!r} no admitido; los válidos son {sorted(EVENTOS)}")
        biblioteca = exigir_forma(e["biblioteca"], str, aqui, "biblioteca", no_vacio=True)
        if biblioteca not in bibliotecas:
            raise ErrorPlaybook(f"{aqui}: la biblioteca {biblioteca!r} no está en 'bibliotecas' ({bibliotecas})")
        funcion = exigir_forma(e["funcion"], str, aqui, "funcion", no_vacio=True)
        if not NOMBRE_DE_FUNCION.match(funcion):
            raise ErrorPlaybook(f"{aqui}: funcion {funcion!r} no es un nombre de función de JavaScript "
                                "(letras, dígitos, _ y $, con puntos entre los espacios de nombres)")
        if "pasar_contexto" in e:
            exigir_forma(e["pasar_contexto"], bool, aqui, "pasar_contexto")
        columna = None
        if evento == "onchange":
            if "columna" not in e:
                raise ErrorPlaybook(f"{aqui}: un evento onchange necesita 'columna'")
            columna = exigir_forma(e["columna"], str, aqui, "columna", no_vacio=True)
            if columna not in columnas:
                raise ErrorPlaybook(f"{aqui}: la columna {columna!r} no está en el formulario")
        elif "columna" in e:
            raise ErrorPlaybook(f"{aqui}: un evento solo lleva 'columna' en onchange (este es {evento})")
        huella = (evento, columna, biblioteca, funcion)
        if huella in vistos:
            raise ErrorPlaybook(f"{aqui}: manejador repetido ({evento} {funcion})")
        vistos.add(huella)


def _validar_estructura(datos, identidad):
    de_varias = "pestanas" in datos
    obligatorias = dict(CLAVES_COMUNES) if de_varias else dict(CLAVES)
    faltan = sorted(set(obligatorias) - set(datos))
    sobran = sorted(set(datos) - set(obligatorias) - set(CLAVES_OPCIONALES))
    if de_varias:
        sobran = sorted(set(sobran) | (set(datos) & set(CLAVES_DE_UNA_PESTANA)))
    if faltan or sobran:
        partes = []
        if faltan:
            partes.append(f"faltan {faltan}")
        if sobran:
            partes.append(f"sobran {sobran}")
        raise ErrorPlaybook(f"el bloque del formulario no tiene las claves esperadas: {'; '.join(partes)}")
    for clave, tipo in obligatorias.items():
        if clave in CLAVES_DE_TEXTO_POR_IDIOMA:
            continue
        exigir_forma(datos[clave], tipo, "el bloque del formulario", clave, no_vacio=(tipo is str))
    for clave, tipo in CLAVES_OPCIONALES.items():
        if clave in datos:
            # Una lista vacía de bibliotecas o de eventos es "ninguno"; una de pestañas, no.
            exigir_forma(datos[clave], tipo, "el bloque del formulario", clave,
                         no_vacio=(clave in ("clase", "pestanas")))
    if datos["tipo"] != TIPO:
        raise ErrorPlaybook(f"el bloque dice tipo = {datos['tipo']!r}, se esperaba {TIPO!r}")

    clase = clase_de(datos)
    if clase not in CLASES:
        raise ErrorPlaybook(
            f"el bloque dice clase = {clase!r}; las que existen son {sorted(CLASES)}")

    exigir_sin_tildes(datos["nombre"], "nombre")
    if len(datos["nombre"]) > LARGO_NOMBRE:
        raise ErrorPlaybook(f"nombre tiene {len(datos['nombre'])} caracteres; el máximo es {LARGO_NOMBRE}")
    idiomas = identidad["idiomas"]

    # Pestañas: las dos formas se validan como una lista de `{titulo, secciones}`.
    if de_varias:
        for i, p in enumerate(datos["pestanas"]):
            aqui = f"la pestaña [{i}]"
            exigir_forma(p, dict, aqui, "pestana")
            faltan_p, sobran_p = sorted({"titulo", "secciones"} - set(p)), sorted(set(p) - {"titulo", "secciones"})
            if faltan_p or sobran_p:
                raise ErrorPlaybook(f"{aqui}: faltan {faltan_p}, sobran {sobran_p}")
            validar_textos_por_idioma(p["titulo"], idiomas, f"pestanas[{i}].titulo")
            exigir_forma(p["secciones"], list, aqui, "secciones", no_vacio=True)
    else:
        validar_textos_por_idioma(datos["pestana"], idiomas, "pestana")
        exigir_forma(datos["secciones"], list, "el bloque del formulario", "secciones", no_vacio=True)

    if clase == "creacion_rapida":
        # Un formulario de creación rápida no tiene encabezado de registro, ni subgrillas, ni
        # controles de web resource ni timeline: la plataforma los ignora. Se rechaza en vez de generarlos y
        # dejar que el usuario descubra en pantalla que la mitad del playbook no hizo nada.
        if datos["encabezado"]:
            raise ErrorPlaybook(
                "un formulario de creación rápida no lleva encabezado: 'encabezado' tiene que ir vacío")
        for i, s in enumerate(secciones_de(datos)):
            if not isinstance(s, dict) or "campos" not in s:
                raise ErrorPlaybook(
                    f"la sección {i} no es de campos; un formulario de creación rápida solo admite campos")

    encabezado = exigir_forma(datos["encabezado"], list, "el bloque del formulario", "encabezado")
    for k, c in enumerate(encabezado):
        _validar_campo(c, "encabezado", k)
    if len(encabezado) > 3:
        # El header de un formulario principal tiene exactamente tres huecos.
        raise ErrorPlaybook(f"el encabezado trae {len(encabezado)} columnas y solo entran 3")
    nombres_del_encabezado = [nombre_de_campo(c) for c in encabezado]
    if len(set(nombres_del_encabezado)) != len(nombres_del_encabezado):
        raise ErrorPlaybook("el encabezado: una columna ya está en el formulario (repetida en el encabezado)")

    vistas, secciones = set(), secciones_de(datos)
    columnas_vistas = set(nombres_del_encabezado)
    hay_timeline = False
    for i, s in enumerate(secciones):
        aqui = f"la sección [{i}]"
        exigir_forma(s, dict, aqui, "seccion")
        sobran = sorted(set(s) - {"titulo", "campos", "subgrilla", "webresource", "timeline"})
        if sobran or "titulo" not in s:
            raise ErrorPlaybook(
                f"{aqui}: se admite {{titulo, campos, subgrilla, webresource, timeline}}; falta 'titulo' o sobra {sobran}")
        validar_textos_por_idioma(s["titulo"], idiomas, f"{aqui}.titulo")
        cuantos = sum(1 for c in ("campos", "subgrilla", "webresource", "timeline") if c in s)
        if cuantos != 1:
            raise ErrorPlaybook(
                f"{aqui}: una sección lleva 'campos', 'subgrilla', 'webresource' O 'timeline', exactamente uno")
        if "timeline" in s:
            if hay_timeline:
                raise ErrorPlaybook(f"{aqui}: un formulario lleva un solo timeline (el control tiene un id fijo)")
            hay_timeline = True
            tl = exigir_forma(s["timeline"], dict, aqui, "timeline")
            faltan_tl, sobran_tl = sorted({"alto"} - set(tl)), sorted(set(tl) - {"alto"})
            if faltan_tl or sobran_tl:
                raise ErrorPlaybook(f"{aqui}.timeline: faltan {faltan_tl}, sobran {sobran_tl}")
            exigir_forma(tl["alto"], int, f"{aqui}.timeline", "alto")
            if not 1 <= tl["alto"] <= ALTO_MAXIMO_TIMELINE:
                raise ErrorPlaybook(f"{aqui}.timeline: 'alto' va en filas, entre 1 y {ALTO_MAXIMO_TIMELINE}")
        if "webresource" in s:
            wr = exigir_forma(s["webresource"], dict, aqui, "webresource")
            faltan_wr = sorted({"nombre", "alto"} - set(wr))
            sobran_wr = sorted(set(wr) - {"nombre", "alto"})
            if faltan_wr or sobran_wr:
                raise ErrorPlaybook(f"{aqui}.webresource: faltan {faltan_wr}, sobran {sobran_wr}")
            exigir_forma(wr["nombre"], str, f"{aqui}.webresource", "nombre", no_vacio=True)
            exigir_forma(wr["alto"], int, f"{aqui}.webresource", "alto")
            if not 1 <= wr["alto"] <= 20:
                raise ErrorPlaybook(f"{aqui}.webresource: 'alto' va en filas, entre 1 y 20")

        if "campos" in s:
            exigir_forma(s["campos"], list, aqui, "campos", no_vacio=True)
            for k, campo in enumerate(s["campos"]):
                _validar_campo(campo, "campos", k)
                columna = nombre_de_campo(campo)
                if columna in columnas_vistas:
                    raise ErrorPlaybook(f"{aqui}: la columna '{columna}' ya está en el formulario")
                columnas_vistas.add(columna)
        elif "webresource" in s or "timeline" in s:
            pass   # ya quedó validado arriba
        else:
            sg = exigir_forma(s["subgrilla"], dict, aqui, "subgrilla")
            faltan_sg = sorted({"tabla", "lookup", "vista"} - set(sg))
            sobran_sg = sorted(set(sg) - {"tabla", "lookup", "vista", "registros_por_pagina"})
            if faltan_sg or sobran_sg:
                raise ErrorPlaybook(f"{aqui}.subgrilla: faltan {faltan_sg}, sobran {sobran_sg}")
            for campo in ("tabla", "lookup", "vista"):
                exigir_forma(sg[campo], str, f"{aqui}.subgrilla", campo, no_vacio=True)
            if "registros_por_pagina" in sg:
                exigir_forma(sg["registros_por_pagina"], int, f"{aqui}.subgrilla", "registros_por_pagina")
                if not REGISTROS_MINIMOS <= sg["registros_por_pagina"] <= REGISTROS_MAXIMOS:
                    raise ErrorPlaybook(f"{aqui}.subgrilla: 'registros_por_pagina' va entre {REGISTROS_MINIMOS} y "
                                        f"{REGISTROS_MAXIMOS} (Learn, \"Add and configure a subgrid component on a form\")")
            huella = (sg["tabla"], sg["lookup"])
            if huella in vistas:
                raise ErrorPlaybook(f"{aqui}: ya hay otra subgrilla sobre {sg['tabla']} por '{sg['lookup']}'")
            vistas.add(huella)

    _validar_bibliotecas_y_eventos(datos, columnas_vistas)


def validar_playbook(datos, identidad):
    _validar_estructura(datos, identidad)


# ---------------------------------------------------------------------------
# Generación del formxml
# ---------------------------------------------------------------------------
def _labels(textos, idiomas):
    """`<labels>` con UNA `<label>` por idioma de la Identidad, en el orden de `idiomas`. `textos` es
    `{"1033": ..., "3082": ...}`."""
    return "<labels>" + "".join(
        f'<label description={quoteattr(textos[str(i)])} languagecode="{i}" />' for i in idiomas) + "</labels>"


def _celda(datos, ruta, columna, etiqueta, classid, deshabilitado, idiomas):
    return (
        f'<cell id="{guid(datos["nombre"], ruta, "cell")}" locklevel="0" colspan="1" rowspan="1" '
        f'labelid="{guid(datos["nombre"], ruta, "label")}">'
        f'{_labels(etiqueta, idiomas)}'
        f'<control id={quoteattr(columna)} classid="{classid}" datafieldname={quoteattr(columna)} '
        f'disabled="{str(bool(deshabilitado)).lower()}" /></cell>')


def _celda_subgrilla(datos, ruta, nombre, etiqueta, tabla_destino, vista_id, relacion, idiomas,
                     registros=REGISTROS_POR_OMISION):
    return (
        f'<cell id="{guid(datos["nombre"], ruta, "cell")}" locklevel="0" colspan="1" rowspan="6" auto="false" '
        f'labelid="{guid(datos["nombre"], ruta, "label")}">'
        f'{_labels(etiqueta, idiomas)}'
        f'<control indicationOfSubgrid="true" id={quoteattr(nombre)} classid="{CONTROL_SUBGRILLA}"><parameters>'
        f"<RecordsPerPage>{registros}</RecordsPerPage><AutoExpand>Fixed</AutoExpand>"
        "<EnableQuickFind>false</EnableQuickFind><EnableViewPicker>false</EnableViewPicker>"
        "<EnableChartPicker>false</EnableChartPicker><ChartGridMode>Grid</ChartGridMode>"
        f"<TargetEntityType>{tabla_destino}</TargetEntityType>"
        f"<ViewId>{vista_id}</ViewId><ViewIds>{vista_id}</ViewIds>"
        f"<RelationshipName>{relacion}</RelationshipName>"
        "</parameters></control></cell>")


def _celda_webresource(datos, ruta, nombre_control, etiqueta, web_resource, alto, idiomas):
    """El control de web resource. `PassParameters` en true es lo que hace que
    la página reciba `id` y `typename` del registro en la cadena de consulta:
    sin eso no sabe qué registro mostrar."""
    return (
        f'<cell id="{guid(datos["nombre"], ruta, "cell")}" locklevel="0" colspan="1" rowspan="{alto}" '
        f'auto="false" showlabel="false" labelid="{guid(datos["nombre"], ruta, "label")}">'
        f'{_labels(etiqueta, idiomas)}'
        f'<control id={quoteattr(nombre_control)} classid="{CONTROL_WEBRESOURCE}"><parameters>'
        f"<Url>{web_resource}</Url>"
        "<PassParameters>true</PassParameters>"
        "<Security>false</Security><Scrolling>auto</Scrolling><Border>false</Border>"
        "</parameters></control></cell>")


def formxml(datos, etiquetas, controles, subgrillas, idiomas):
    """El `formxml` completo. `etiquetas` (`{columna: {"1033": ..., "3082": ...}}`) y `controles` vienen de la
    metadata; `subgrillas` trae, por sección, el id de vista y el nombre de relación ya resueltos contra el
    entorno; `idiomas` son los de la Identidad: cada etiqueta sale una vez por idioma, en ese orden."""
    solo_lectura = datos["solo_lectura"]
    por_idioma_vacio = {str(i): "" for i in idiomas}   # la celda de relleno también lleva una etiqueta vacía por idioma
    partes = ['<form headerdensity="HighWithControls"><tabs>']

    i = 0   # índice GLOBAL de sección: da el id de la sección y el nombre de la subgrilla, no se reinicia por pestaña
    for k, pestana in enumerate(pestanas_de(datos)):
        # La primera pestaña conserva los ids de siempre (un formulario de una pestaña no cambia al sumar esta función).
        ruta_tab = ("tab",) if k == 0 else ("tab", k)
        partes += [f'<tab verticallayout="true" id="{guid(datos["nombre"], *ruta_tab)}" IsUserDefined="1" '
                   f'labelid="{guid(datos["nombre"], *ruta_tab, "label")}">',
                   _labels(pestana["titulo"], idiomas),
                   '<columns><column width="100%"><sections>']
        for s in pestana["secciones"]:
            ruta_sec = f"sec{i}"
            partes.append(
                f'<section showlabel="true" showbar="false" IsUserDefined="0" '
                f'id="{guid(datos["nombre"], ruta_sec)}" labelid="{guid(datos["nombre"], ruta_sec, "label")}" '
                f'columns="1" labelwidth="115" celllabelalignment="Left" celllabelposition="Left">'
                f'{_labels(s["titulo"], idiomas)}<rows>')
            if "campos" in s:
                for j, campo in enumerate(s["campos"]):
                    columna = nombre_de_campo(campo)
                    partes.append("<row>" + _celda(datos, f"{ruta_sec}.{j}", columna, etiquetas[columna],
                                                   controles[columna], _solo_lectura_de(campo, solo_lectura),
                                                   idiomas) + "</row>")
            elif "webresource" in s:
                wr = s["webresource"]
                partes.append("<row>" + _celda_webresource(
                    datos, f"{ruta_sec}.wr", f"WebResource_{i}", s["titulo"],
                    wr["nombre"], wr["alto"], idiomas) + "</row>")
            elif "timeline" in s:
                partes.append("<row>" + _celda_timeline(
                    datos, f"{ruta_sec}.timeline", s["titulo"], s["timeline"]["alto"], idiomas) + "</row>")
            else:
                sg = s["subgrilla"]
                resuelta = subgrillas[i]
                partes.append("<row>" + _celda_subgrilla(
                    datos, f"{ruta_sec}.grid", f"Subgrid_{i}", s["titulo"], sg["tabla"],
                    resuelta["vista_id"], resuelta["relacion"], idiomas,
                    sg.get("registros_por_pagina", REGISTROS_POR_OMISION)) + "</row>")
            partes.append("</rows></section>")
            i += 1
        partes.append("</sections></column></columns></tab>")
    partes.append("</tabs>")

    if clase_de(datos) == "creacion_rapida":
        # Sin `<header>`: un formulario de creación rápida no muestra encabezado de registro.
        # Es justamente lo que lo hace chico.
        partes += [_xml_bibliotecas_y_eventos(datos), "</form>"]
        return "".join(partes)

    # El header tiene exactamente tres huecos; los que no se usan van vacíos.
    partes.append(f'<header id="{guid(datos["nombre"], "header")}" celllabelposition="Top" columns="111" '
                  'labelwidth="115" celllabelalignment="Left"><rows><row>')
    for k in range(3):
        if k < len(datos["encabezado"]):
            campo = datos["encabezado"][k]
            columna = nombre_de_campo(campo)
            partes.append(_celda(datos, f"hdr{k}", columna, etiquetas[columna], controles[columna],
                                 _solo_lectura_de(campo, solo_lectura), idiomas))
        else:
            partes.append(f'<cell id="{guid(datos["nombre"], f"hdr{k}")}" showlabel="false" '
                          f'labelid="{guid(datos["nombre"], f"hdr{k}", "label")}">'
                          f'{_labels(por_idioma_vacio, idiomas)}</cell>')
    partes.append("</row></rows></header>")
    # Después del encabezado y en este orden: así los trae un formulario real con bibliotecas y eventos (Main de `contact`).
    partes += [_xml_bibliotecas_y_eventos(datos), "</form>"]
    return "".join(partes)


def _celda_timeline(datos, ruta, etiqueta, alto, idiomas):
    """El control Timeline (`notescontrol`), tal cual lo trae un Main real: celda sin etiqueta visible, con `rowspan`, y el control
    sin parámetros. La configuración fina del timeline (qué actividades muestra, si muestra notas) NO está en este XML."""
    return (
        f'<cell id="{guid(datos["nombre"], ruta, "cell")}" locklevel="0" colspan="1" rowspan="{alto}" auto="false" '
        f'showlabel="false" labelid="{guid(datos["nombre"], ruta, "label")}">'
        f'{_labels(etiqueta, idiomas)}'
        f'<control id="{ID_DEL_TIMELINE}" classid="{CONTROL_TIMELINE}" /></cell>')


def _manejadores_declarados(datos):
    """`[(evento, columna|None, biblioteca, funcion, pasar_contexto)]` en el orden del playbook."""
    return [(e["evento"], e.get("columna"), e["biblioteca"], e["funcion"], bool(e.get("pasar_contexto", False)))
            for e in datos.get("eventos", [])]


def _xml_bibliotecas_y_eventos(datos):
    """`<formLibraries>` y `<events>` (vacío si el playbook no declara bibliotecas ni eventos). Un `<event>` por (evento, columna),
    con sus manejadores en el orden del playbook. Los GUID son deterministas."""
    nombre = datos["nombre"]
    salida_xml = []
    bibliotecas = datos.get("bibliotecas", [])
    if bibliotecas:
        salida_xml.append("<formLibraries>" + "".join(
            f'<Library name={quoteattr(b)} libraryUniqueId="{guid(nombre, "lib", b)}" />' for b in bibliotecas)
            + "</formLibraries>")
    grupos = {}
    for evento, columna, biblioteca, funcion, pasar in _manejadores_declarados(datos):
        grupos.setdefault((evento, columna), []).append((biblioteca, funcion, pasar))
    if grupos:
        salida_xml.append("<events>")
        for (evento, columna), manejadores in grupos.items():
            atributo = f" attribute={quoteattr(columna)}" if columna else ""
            # `active`: el de cambio de columna va activo; los de formulario (carga y guardado), no.
            salida_xml.append(f'<event name="{evento}" application="false" '
                              f'active="{"true" if columna else "false"}"{atributo}><Handlers>')
            for biblioteca, funcion, pasar in manejadores:
                salida_xml.append(
                    f'<Handler functionName={quoteattr(funcion)} libraryName={quoteattr(biblioteca)} '
                    f'handlerUniqueId="{guid(nombre, "handler", evento, columna or "", biblioteca, funcion)}" '
                    f'enabled="true" parameters="" passExecutionContext="{str(pasar).lower()}" />')
            salida_xml.append("</Handlers></event>")
        salida_xml.append("</events>")
    return "".join(salida_xml)


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
def _columnas_de(dv, tabla):
    """Tipo y etiquetas de cada columna, desde la metadata. Las etiquetas salen del `DisplayName`, una por idioma
    (`{LCID: texto}`): una escrita a mano en el playbook se despega de la tabla."""
    ruta = (f"EntityDefinitions(LogicalName='{tabla}')/Attributes"
            "?$select=LogicalName,AttributeTypeName,DisplayName")
    cuerpo = leer_entorno(dv, ruta, C_COLUMNAS, admite_404=True)
    if cuerpo is None:
        raise Bloqueado(f"la tabla '{tabla}' no existe en el entorno")
    tipos, etiquetas = {}, {}
    for a in exigir_forma(cuerpo.get("value"), [dict], C_COLUMNAS, "value", no_vacio=True):
        nombre = exigir_forma(a.get("LogicalName"), str, C_COLUMNAS, "LogicalName", no_vacio=True)
        tipos[nombre] = (a.get("AttributeTypeName") or {}).get("Value")
        exigir_etiqueta(a.get("DisplayName"), C_COLUMNAS, f"{nombre}.DisplayName", permite_nulo=True)
        etiquetas[nombre] = textos_del_entorno(a.get("DisplayName"))
    return tipos, etiquetas


def _etiquetas_de_los_campos(declaradas, etiquetas, idiomas):
    """`{columna: {"1033": texto, ...}}` de las columnas que el formulario usa. Que la tabla no tenga la etiqueta de
    una columna en algún idioma de la Identidad es `Bloqueado`: la consulta respondió bien, falta un dato de NEGOCIO
    (de la tabla, no del formulario) y no se puede armar la etiqueta embebida de ese idioma sin inventarla."""
    faltan = [f"'{c}' en {i}" for c in declaradas for i in idiomas if not (etiquetas[c].get(i) or "").strip()]
    if faltan:
        raise Bloqueado(
            f"la tabla no tiene la etiqueta (DisplayName) de {', '.join(faltan)}; el formulario embebe una etiqueta por "
            "idioma de la Identidad y no la inventa: hay que completarla en la tabla")
    return {c: {str(i): etiquetas[c][i] for i in idiomas} for c in declaradas}


def _vista_id(dv, tabla, nombre_vista):
    ruta = f"savedqueries?$select=savedqueryid&$filter=name eq '{nombre_vista}' and returnedtypecode eq '{tabla}'"
    cuerpo = leer_entorno(dv, ruta, C_VISTA)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_VISTA, "value")
    if len(filas) != 1:
        raise Bloqueado(
            f"la subgrilla pide la vista '{nombre_vista}' sobre '{tabla}' y hay {len(filas)}; hay que crearla primero")
    return "{" + exigir_forma(filas[0].get("savedqueryid"), str, C_VISTA, "savedqueryid", no_vacio=True).upper() + "}"


def _relacion(dv, tabla_padre, tabla_hija, lookup):
    ruta = (f"EntityDefinitions(LogicalName='{tabla_padre}')/OneToManyRelationships"
            f"?$select=SchemaName,ReferencingEntity,ReferencingAttribute")
    cuerpo = leer_entorno(dv, ruta, C_RELACION)
    for r in exigir_forma(cuerpo.get("value"), [dict], C_RELACION, "value"):
        if r.get("ReferencingEntity") == tabla_hija and r.get("ReferencingAttribute") == lookup:
            return exigir_forma(r.get("SchemaName"), str, C_RELACION, "SchemaName", no_vacio=True)
    raise Bloqueado(
        f"no hay relación de '{tabla_padre}' a '{tabla_hija}' por el lookup '{lookup}'; la subgrilla no se puede armar")


def _exigir_webresource(dv, nombre, tipo=TIPO_HTML):
    """El web resource que el formulario referencia tiene que existir y ser del tipo que se usa: HTML en un control
    de sección, JavaScript en una biblioteca. Un nombre que no existe no da error al guardar el formulario."""
    ruta = f"webresourceset?$select=name,webresourcetype&$filter=name eq '{nombre}'"
    filas = exigir_forma(leer_entorno(dv, ruta, C_WEBRESOURCE).get("value"), [dict], C_WEBRESOURCE, "value")
    if len(filas) != 1:
        raise Bloqueado(
            f"el formulario referencia el web resource '{nombre}' y en el entorno hay {len(filas)}; "
            "hay que crearlo primero (`web_resource.py`)")
    real = exigir_forma(filas[0].get("webresourcetype"), int, C_WEBRESOURCE, "webresourcetype")
    if real != tipo:
        if tipo == TIPO_JS:
            raise Bloqueado(
                f"el web resource '{nombre}' no es JavaScript (webresourcetype {real}): "
                "una biblioteca de formulario solo puede ser un script")
        raise Bloqueado(
            f"el web resource '{nombre}' no es HTML (webresourcetype "
            f"{real}): un control de formulario solo muestra HTML")


def _exigir_actividades(dv, tabla):
    """El timeline muestra las actividades (los correos) vinculadas a la fila: sin actividades habilitadas en la tabla el
    control aparece vacío y nada falla al guardar el formulario."""
    ruta = f"EntityDefinitions(LogicalName='{tabla}')?$select=HasActivities"
    cuerpo = leer_entorno(dv, ruta, C_TABLA, admite_404=True)
    if cuerpo is None:
        raise Bloqueado(f"la tabla '{tabla}' no existe en el entorno")
    if not exigir_forma(cuerpo.get("HasActivities"), bool, C_TABLA, "HasActivities"):
        raise Bloqueado(f"el formulario lleva un timeline y la tabla '{tabla}' no tiene actividades habilitadas "
                        "(HasActivities = false): el timeline no mostraría los correos")


def _leer_formulario(dv, tabla, tipo):
    # El filtro por `type` es obligatorio: una tabla puede tener un formulario principal Y uno
    # de creación rápida, y leer el que no es haría que la herramienta crea que el suyo no
    # existe y lo duplique.
    ruta = ("systemforms?$select=formid,name,description,formxml,type,formactivationstate,ismanaged"
            f"&$filter=objecttypecode eq '{tabla}' and type eq {tipo}")
    cuerpo = leer_entorno(dv, ruta, C_FORM)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_FORM, "value")
    if len(filas) > 1:
        raise ErrorEntorno(
            f"{C_FORM} devolvió {len(filas)} formularios principales de '{tabla}'; se esperaba uno solo. "
            "Con dos Main compitiendo, la plataforma elige uno y el playbook deja de mandar")
    return filas[0] if filas else None


def _normalizar_xml(texto):
    return "".join((texto or "").split())


def _tabla_en_la_solucion(dv, solution_id, metadata_id):
    """Un formulario no tiene fila propia en `solutioncomponents`: viaja dentro
    de su tabla, igual que una vista o una clave alternativa."""
    ruta = ("solutioncomponents?$select=componenttype,rootcomponentbehavior,objectid"
            f"&$filter=_solutionid_value eq {solution_id} and objectid eq {metadata_id} "
            f"and componenttype eq {COMPONENTE_TABLA}")
    cuerpo = leer_entorno(dv, ruta, C_COMPONENTES)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_COMPONENTES, "value")
    return filas[0] if filas else None


def _publicar(dv, tabla, dormir):
    cuerpo = {"ParameterXml": f"<importexportxml><entities><entity>{tabla}</entity></entities></importexportxml>"}
    return escribir_metadatos(dv, "POST", "PublishXml", cuerpo, dormir=dormir)


def _etiquetas_del_xml(formxml):
    """`{(tag, id): (dónde, {languagecode: texto})}` de cada pestaña, sección y celda del `formxml`, tal cual están
    (todos los idiomas que traiga, también los que la Identidad no declara). Lanza `ET.ParseError` si no es XML."""
    raiz = ET.fromstring(formxml or "")
    resultado, secciones = {}, 0
    for el in raiz.iter():
        if el.tag not in ("tab", "section", "cell"):
            continue
        if el.tag == "section":
            secciones += 1
        labels = el.find("labels")
        if labels is None:
            continue
        control = el.find("control")
        donde = {"tab": "la pestaña", "section": f"la sección [{secciones - 1}]"}.get(
            el.tag, f"el control '{control.get('id')}'" if control is not None else "una celda de relleno")
        resultado[(el.tag, el.get("id"))] = (
            donde, {l.get("languagecode"): l.get("description") for l in labels.findall("label")})
    return resultado


def _diferencias_de_etiquetas(esperado_xml, real_xml):
    """Las etiquetas embebidas de CADA idioma, en los dos sentidos: una que falta o difiere y una que sobra (en un
    idioma que la Identidad no declara). La estructura distinta (celdas que faltan o sobran) la cubre la comparación
    del XML entero."""
    try:
        reales = _etiquetas_del_xml(real_xml)
    except ET.ParseError:
        return ["el formxml del entorno no es XML válido"]
    difs = []
    for clave, (donde, textos) in _etiquetas_del_xml(esperado_xml).items():
        if clave not in reales:
            continue
        actuales = reales[clave][1]
        for lcid, texto in textos.items():
            if lcid not in actuales:
                difs.append(f"{donde}: falta la etiqueta {lcid}")
            elif actuales[lcid] != texto:
                difs.append(f"{donde}: la etiqueta {lcid} es {actuales[lcid]!r} y el playbook dice {texto!r}")
        for lcid in actuales:
            if lcid not in textos:
                difs.append(f"{donde}: trae una etiqueta en el idioma {lcid}, que la Identidad no declara")
    return difs


def _scripts_del_xml(formxml):
    """`(bibliotecas, manejadores)` que trae el `formxml`, solo los de usuario (`<Library>` y `<Handlers>/<Handler>`; los
    `InternalHandlers` son de la plataforma). Cada manejador es `(evento, columna, biblioteca, funcion, pasar_contexto)`."""
    raiz = ET.fromstring(formxml or "")
    bibliotecas = [l.get("name") for l in raiz.findall("./formLibraries/Library")]
    manejadores = []
    for e in raiz.findall("./events/event"):
        for h in e.findall("./Handlers/Handler"):
            manejadores.append((e.get("name"), e.get("attribute"), h.get("libraryName"), h.get("functionName"),
                                (h.get("passExecutionContext") or "").lower() == "true"))
    return bibliotecas, manejadores


def _nombre_de_manejador(m):
    evento, columna, _, funcion, _ = m
    return f"{evento}[{columna}] {funcion}" if columna else f"{evento} {funcion}"


def _diferencias_de_scripts(esperado_xml, real_xml):
    """Bibliotecas y manejadores, en los dos sentidos y por nombre: lo que falta, lo que sobra y el que difiere solo en
    pasar el contexto (que es justo lo que decide si una función de guardado recibe el evento)."""
    try:
        reales = _scripts_del_xml(real_xml)
    except ET.ParseError:
        return []   # `_diferencias_de_etiquetas` ya denuncia el XML inválido
    libs_e, hand_e = _scripts_del_xml(esperado_xml)
    libs_r, hand_r = reales
    difs = [f"falta la biblioteca {b!r}" for b in libs_e if b not in libs_r]
    difs += [f"sobra la biblioteca {b!r}" for b in libs_r if b not in libs_e]
    clave = lambda m: m[:4]   # noqa: E731  (evento, columna, biblioteca, funcion)
    reales_por_clave = {clave(m): m for m in hand_r}
    esperados_por_clave = {clave(m): m for m in hand_e}
    for m in hand_e:
        real = reales_por_clave.get(clave(m))
        if real is None:
            difs.append(f"falta el manejador {_nombre_de_manejador(m)}")
        elif real[4] != m[4]:
            difs.append(f"el manejador {_nombre_de_manejador(m)} tiene passExecutionContext = {real[4]} "
                        f"y el playbook dice {m[4]}")
    difs += [f"sobra el manejador {_nombre_de_manejador(m)}" for m in hand_r if clave(m) not in esperados_por_clave]
    return difs


def _diferencias(datos, form, xml):
    difs, corregibles = [], {}
    for campo, valor in (("name", datos["nombre"]), ("description", datos["descripcion"])):
        if form.get(campo) != valor:
            difs.append(f"{campo} es {form.get(campo)!r} y el playbook dice {valor!r}")
            corregibles[campo] = valor
    if _normalizar_xml(form.get("formxml")) != _normalizar_xml(xml):
        difs.extend(_diferencias_de_etiquetas(xml, form.get("formxml")))
        difs.extend(_diferencias_de_scripts(xml, form.get("formxml")))
        difs.append("el formxml no coincide con el que genera el playbook")
        corregibles["formxml"] = xml
    if form.get("formactivationstate") != ACTIVO:
        difs.append(f"el formulario está inactivo (formactivationstate {form.get('formactivationstate')!r})")
        corregibles["formactivationstate"] = ACTIVO
    if form.get("ismanaged"):
        difs.append("el formulario está managed en este entorno; no se toca desde acá")
        corregibles.clear()
    return difs, corregibles


def _contra_entorno(dv, datos, identidad, solo_verificar, completar, componente, dormir):
    solucion = identidad["solucion"]
    solution_id = comprobar_solucion_e_idioma(dv, identidad)

    ruta_tabla = f"EntityDefinitions(LogicalName='{datos['tabla']}')?$select=MetadataId"
    cuerpo = leer_entorno(dv, ruta_tabla, C_TABLA, admite_404=True)
    if cuerpo is None:
        raise Bloqueado(f"la tabla '{datos['tabla']}' no existe en el entorno")
    metadata_id = exigir_forma(cuerpo.get("MetadataId"), str, C_TABLA, "MetadataId", no_vacio=True)

    tipos, etiquetas = _columnas_de(dv, datos["tabla"])
    declaradas = columnas_declaradas(datos)
    faltantes = [c for c in declaradas if c not in tipos]
    if faltantes:
        raise Bloqueado(f"el formulario declara columnas que la tabla no tiene: {faltantes}")
    sin_control = sorted({tipos[c] for c in declaradas if tipos[c] not in CONTROL_POR_TIPO})
    if sin_control:
        raise Bloqueado(
            f"no se sabe qué control usa un tipo de columna {sin_control}; los conocidos son "
            f"{sorted(CONTROL_POR_TIPO)}. No se inventa un classid: la plataforma lo guarda tal cual y se ve roto en la app")
    controles = {c: CONTROL_POR_TIPO[tipos[c]] for c in declaradas}
    etiquetas = _etiquetas_de_los_campos(declaradas, etiquetas, identidad["idiomas"])

    subgrillas = {}
    for i, s in enumerate(secciones_de(datos)):
        if "timeline" in s:
            _exigir_actividades(dv, datos["tabla"])
        elif "subgrilla" in s:
            sg = s["subgrilla"]
            subgrillas[i] = {"vista_id": _vista_id(dv, sg["tabla"], sg["vista"]),
                             "relacion": _relacion(dv, datos["tabla"], sg["tabla"], sg["lookup"])}
        elif "webresource" in s:
            # Un `<Url>` que no existe no da error al guardar: el formulario
            # abre con un recuadro vacío y nadie se entera hasta que alguien
            # lo mira. Se comprueba antes.
            _exigir_webresource(dv, s["webresource"]["nombre"])
    for biblioteca in datos.get("bibliotecas", []):
        # Una biblioteca que no existe tampoco falla al guardar: el formulario carga y el evento no hace nada.
        _exigir_webresource(dv, biblioteca, TIPO_JS)

    xml = formxml(datos, etiquetas, controles, subgrillas, identidad["idiomas"])
    form = _leer_formulario(dv, datos["tabla"], tipo_de_formulario(datos))
    hubo_escritura = False
    hubo_alta = False

    if form is None:
        if solo_verificar:
            return "difiere", componente, f"la tabla '{datos['tabla']}' no tiene formulario principal"
        cuerpo = {"name": datos["nombre"], "description": datos["descripcion"],
                  "objecttypecode": datos["tabla"], "type": tipo_de_formulario(datos),
                  "formxml": xml, "formactivationstate": ACTIVO}
        est, resp, _ = escribir_metadatos(dv, "POST", "systemforms", cuerpo, dormir=dormir, solucion=solucion)
        if est not in (200, 201, 204):
            return "error", componente, f"el alta devolvió HTTP {est} (se esperaba 204): {resp}"
        form = _leer_formulario(dv, datos["tabla"], tipo_de_formulario(datos))
        if form is None:
            return "error", componente, "el alta no dio error pero el formulario no aparece al releer"
        hubo_escritura = True
        hubo_alta = True
    else:
        difs, corregibles = _diferencias(datos, form, xml)
        if corregibles and not solo_verificar:
            id_form = exigir_forma(form.get("formid"), str, C_FORM, "formid", no_vacio=True)
            est, resp, _ = escribir_metadatos(
                dv, "PATCH", f"systemforms({id_form})", corregibles, dormir=dormir, solucion=solucion)
            if est not in (200, 204):
                return "error", componente, f"la corrección devolvió HTTP {est} (se esperaba 204): {resp}"
            est, resp, _ = _publicar(dv, datos["tabla"], dormir)
            if est not in (200, 204):
                return "error", componente, f"se corrigió pero la publicación devolvió HTTP {est}: {resp}"
            form = _leer_formulario(dv, datos["tabla"], tipo_de_formulario(datos))
            hubo_escritura = True

    id_form = exigir_forma(form.get("formid"), str, C_FORM, "formid", no_vacio=True)
    difs, _ = _diferencias(datos, form, xml)
    fila = _tabla_en_la_solucion(dv, solution_id, metadata_id)
    if fila is None:
        difs.append(f"la tabla '{datos['tabla']}' no figura en la solución (solutionid {solution_id}), "
                    "así que el formulario no viajaría con ella")
    elif fila.get("rootcomponentbehavior") != INCLUYE_SUBCOMPONENTES:
        difs.append(f"la tabla está en la solución con rootcomponentbehavior {fila.get('rootcomponentbehavior')!r} "
                    f"y no {INCLUYE_SUBCOMPONENTES}: el formulario no viajaría con ella")

    if difs:
        estado = "error" if hubo_escritura else "difiere"
        prefijo = "se escribió pero no quedó bien: " if hubo_escritura else ""
        return estado, componente, prefijo + "; ".join(difs)

    cuantas = sum(len(s.get("campos", [])) for s in secciones_de(datos))
    return estado_de_escritura(hubo_alta, hubo_escritura), componente, (
        f"formid {id_form}, {len(pestanas_de(datos))} pestañas, {len(secciones_de(datos))} secciones, {cuantas} campos, "
        f"{len(subgrillas)} subgrillas, {len(datos['encabezado'])} en el encabezado, "
        f"{len(datos.get('bibliotecas', []))} bibliotecas, {len(datos.get('eventos', []))} manejadores"
        f"{', todo de solo lectura' if datos['solo_lectura'] else ''}; viaja dentro de la tabla")


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
        paso = "validar el bloque del formulario"
        validar_playbook(datos, identidad)
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
            "uso incorrecto: formulario.py <playbook.md> [--solo-verificar] [--completar]"
            + (f"; banderas desconocidas: {desconocidas}" if desconocidas else ""),
        )
    from dataverse_api import Dataverse

    estado, componente, detalle = construir(
        rutas[0], "--solo-verificar" in banderas, Dataverse, completar="--completar" in banderas)
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
