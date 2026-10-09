#!/usr/bin/env python3
"""Aplica a la solución los cambios que SOLO se pueden hacer por
`ImportSolution`, desde un playbook de tipo `importacion`.

    python3 herramientas/construir/importar_solucion.py <playbook.md> [--solo-verificar] [--forzar]

**Por qué existe.** Hay componentes que la Web API no sabe escribir:

- Las TABLAS de una app model-driven. `AddAppComponents` con
  `@odata.type: Microsoft.Dynamics.CRM.entity` devuelve 204 y no agrega nada:
  siempre deja una referencia a la tabla de metadatos `entity`, ignorando el
  GUID (reproducido limpio en Dev el 2026-09-22, en una solución ZZ nueva).
  `RemoveAppComponents` tampoco borra, y `DELETE appmodulecomponents(id)`
  responde que el método no existe para ese tipo. Y `ValidateApp` pasa en
  verde con la app vacía.
- Los COMANDOS clásicos (`RibbonDiffXml`), que no tienen tabla propia.

El único camino es exportar la solución, editar `customizations.xml` y
reimportarla.

**Cómo minimiza el riesgo.** Reimportar la solución entera es la operación más
pesada del proyecto, así que:

1. NO se reparsea el XML. Un `ElementTree` de 1,1 MB reordena atributos y
   reescribe namespaces, y todo eso viaja de vuelta al entorno. Acá se hace
   una sustitución quirúrgica por índices de texto: el resto del archivo sale
   byte por byte igual a como entró.
2. Es idempotente: si lo que hay ya coincide con lo que pide el playbook, NO
   se importa nada y se informa `ya_existia`. **Esa comprobación mira lo que
   FALTA, no lo que SOBRA**: si se SACA un sufijo de `ocultar`, el
   `HideCustomAction` sigue en la solución y la herramienta igual dice
   `ya_existia`. Para eso está `--forzar`, que reimporta sin preguntar. Esto ya
   dejó a varias tablas de catálogo sin el botón "Nuevo" durante horas, y la
   herramienta las declaraba en verde.
3. Verifica DESPUÉS, contra el entorno, releyendo lo que quedó. Un `204` de
   `ImportSolution` no prueba nada.

Última línea de la salida: JSON de una línea con `estado`
(creado | ya_existia | difiere | bloqueado | error), `componente`, `detalle`.
"""
import base64
import io
import os
import re
import sys
import uuid
import zipfile

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

from _comun import (  # noqa: E402
    Bloqueado,
    ErrorEntorno,
    ErrorPlaybook,
    Rastro,
    comprobar_solucion_e_idioma,
    dividir_secciones,
    exigir_forma,
    leer_entorno,
    leer_texto,
    obtener_componente,
    obtener_identidad,
    salida,
)

TIPO = "importacion"

COMPONENTE_TABLA = 1
COMPONENTE_SITEMAP = 62

# Sin `idiomas` propio: los idiomas del ribbon son los de la Identidad (`idiomas`), la única lista de idiomas del proyecto.
CLAVES = {"tipo": str, "cambios": list}
# Opcionales, a nivel del bloque completo (no de un cambio): sirven para que
# `--solo-verificar` denuncie una tabla de la app que se quedó SIN bloque
# `ribbon` en este playbook (una tabla puede entrar a la app y no agregarse nunca
# al playbook de comandos; nada lo dice hasta que alguien mira la barra real,
# semanas después). `app` es el
# `uniquename` de la app model-driven CONTRA la que comparar (se lee su lista
# real de tablas por `RetrieveAppComponents`, no lo que el playbook DICE que
# tiene: la fuente de verdad es la plataforma). `tablas_excluidas_de_ocultar`
# es la lista, chica y explícita, de tablas de la app que a propósito NO
# llevan bloque `ribbon` (p. ej. una tabla técnica que solo se abre en un diálogo
# de creación rápida, nunca como grilla con barra de comandos). Omitir 'app' apaga la
# denuncia entera: un playbook que no lo declara no se compara contra nada.
CLAVES_OPCIONALES = {"app": str, "tablas_excluidas_de_ocultar": list}
CLASES = ("app_componentes", "ribbon")

CLAVES_POR_CLASE = {
    "app_componentes": ["app", "clase", "sitemap", "tablas"],
    "ribbon": ["biblioteca", "botones", "clase", "ocultar", "tabla"],
}
CLAVES_BOTON = ["etiqueta", "icono", "id", "tooltip", "ubicaciones", "secuencia"]
# Un botón lleva EXACTAMENTE una de las dos: `funcion` (botón común) o `menu` (menú desplegable, `FlyoutAnchor`).
# Ver `ribbondiffxml`.
ACCIONES_BOTON = ["funcion", "menu"]
CLAVES_OPCION_MENU = ["etiqueta", "funcion", "id", "secuencia", "tooltip"]
# `regla` es OPCIONAL: la función JS que decide si el botón corresponde para lo
# seleccionado. Sin ella el botón se muestra siempre, que es como nacieron los ocho.
CLAVES_BOTON_OPCIONALES = ["regla", "idioma_del_texto", "etiquetas_localizadas"]
UBICACIONES = ("HomepageGrid", "SubGrid", "Form")

C_APP = "la consulta de la app (GET appmodules)"
C_COMPONENTES = "la consulta de los componentes de la app (GET RetrieveAppComponents)"
C_TRABAJO = "la consulta del trabajo de importación (GET importjobs)"
C_RIBBON = "la consulta del ribbon de una tabla (GET RetrieveEntityRibbon)"

# `RetrieveEntityRibbon` puede traer los menús nuevos y la app seguir mostrando la barra vieja. La app
# lee la metadata COMPILADA (`GetClientMetadata`), que ni importar ni publicar regeneran, y esta herramienta no puede
# regenerarla (no hay API documentada). Por eso la verificación de arriba no alcanza y el éxito dice el paso que falta.
# Receta: power-platform-construir, app-model-driven/patrones.md §2.4.
AVISO_REGENERAR = (
    " | PASO MANUAL: la app no muestra el ribbon nuevo hasta regenerar la metadata compilada. Abrir la app con "
    "'&ribbondebug=true' en la URL, Command Checker > Regenerate ribbon metadata (rol System Administrator o "
    "System Customizer). Hacerlo DESPUÉS de la última importación de ribbon: la regeneración compila lo que hay en ese "
    "momento")
C_STEPS = "la consulta de los steps de la solución (GET sdkmessageprocessingsteps)"

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def validar_playbook(datos, identidad):
    presentes = set(datos)
    faltan = sorted(set(CLAVES) - presentes)
    sobran = sorted(presentes - set(CLAVES) - set(CLAVES_OPCIONALES))
    if faltan or sobran:
        partes = []
        if faltan:
            partes.append(f"faltan {faltan}")
        if sobran:
            partes.append(f"sobran {sobran}")
        raise ErrorPlaybook(f"el bloque de la importación no tiene las claves esperadas: {'; '.join(partes)}")
    if datos["tipo"] != TIPO:
        raise ErrorPlaybook(f"el bloque dice tipo = {datos['tipo']!r}, se esperaba {TIPO!r}")

    idiomas = identidad["idiomas"]
    cambios = exigir_forma(datos["cambios"], list, "el bloque de la importación", "cambios", no_vacio=True)
    apps, tablas_ribbon = set(), set()
    prefijo = identidad["prefijo"] + "_"

    if "tablas_excluidas_de_ocultar" in datos and "app" not in datos:
        raise ErrorPlaybook(
            "'tablas_excluidas_de_ocultar' no tiene sentido sin 'app': no hay contra qué lista de "
            "tablas de la app compararla")
    if "app" in datos:
        exigir_forma(datos["app"], str, "el bloque de la importación", "app", no_vacio=True)
    if "tablas_excluidas_de_ocultar" in datos:
        excluidas = exigir_forma(datos["tablas_excluidas_de_ocultar"], [str], "el bloque de la importación",
                                 "tablas_excluidas_de_ocultar")
        fuera = [t for t in excluidas if not t.startswith(prefijo)]
        if fuera:
            raise ErrorPlaybook(
                f"'tablas_excluidas_de_ocultar' {fuera} no llevan el prefijo del publisher '{prefijo}'")
        if len(set(excluidas)) != len(excluidas):
            raise ErrorPlaybook("'tablas_excluidas_de_ocultar' tiene repetidos")
    for i, cambio in enumerate(cambios):
        donde = f"el cambio [{i}]"
        exigir_forma(cambio, dict, donde, "cambio")
        clase = cambio.get("clase")
        if clase not in CLASES:
            raise ErrorPlaybook(f"{donde}: 'clase' es {clase!r}; las clases admitidas son {list(CLASES)}")
        if sorted(cambio) != sorted(CLAVES_POR_CLASE[clase]):
            raise ErrorPlaybook(f"{donde}: un cambio {clase!r} admite exactamente "
                                f"{{{', '.join(CLAVES_POR_CLASE[clase])}}}")
        if clase == "app_componentes":
            for campo in ("app", "sitemap"):
                exigir_forma(cambio[campo], str, donde, campo, no_vacio=True)
            tablas = exigir_forma(cambio["tablas"], [str], donde, "tablas", no_vacio=True)
            if len(set(tablas)) != len(tablas):
                raise ErrorPlaybook(f"{donde}: la lista de tablas tiene repetidos")
            fuera = [t for t in tablas if not t.startswith(prefijo)]
            if fuera:
                raise ErrorPlaybook(f"{donde}: {fuera} no llevan el prefijo del publisher '{prefijo}'")
            if cambio["app"] in apps:
                raise ErrorPlaybook(f"{donde}: la app '{cambio['app']}' aparece en más de un cambio")
            apps.add(cambio["app"])
        else:
            _validar_ribbon(cambio, donde, identidad, idiomas, tablas_ribbon)


def _validar_ribbon(cambio, donde, identidad, idiomas, tablas_ribbon):
    """Un cambio `ribbon` reemplaza el `<RibbonDiffXml>` ENTERO de su tabla, así
    que tiene que traer TODO lo que esa tabla debe tener: si la misma tabla
    apareciera dos veces, el segundo cambio borraría lo del primero."""
    prefijo = identidad["prefijo"] + "_"
    exigir_forma(cambio["tabla"], str, donde, "tabla", no_vacio=True)
    exigir_forma(cambio["biblioteca"], str, donde, "biblioteca", no_vacio=True)
    if not cambio["tabla"].startswith(prefijo):
        raise ErrorPlaybook(f"{donde}: la tabla '{cambio['tabla']}' no lleva el prefijo '{prefijo}'")
    if cambio["tabla"] in tablas_ribbon:
        raise ErrorPlaybook(
            f"{donde}: la tabla '{cambio['tabla']}' ya tiene un cambio de ribbon; un cambio reemplaza "
            "el RibbonDiffXml entero, así que el segundo borraría lo del primero")
    tablas_ribbon.add(cambio["tabla"])

    ocultar = exigir_forma(cambio["ocultar"], [str], donde, "ocultar")
    fuera = [s for s in ocultar if not s.startswith(".")]
    if fuera:
        raise ErrorPlaybook(f"{donde}: los sufijos a ocultar empiezan con punto; {fuera} no")
    if len(set(ocultar)) != len(ocultar):
        raise ErrorPlaybook(f"{donde}: la lista 'ocultar' tiene repetidos")

    botones = exigir_forma(cambio["botones"], list, donde, "botones")
    if not botones and not ocultar:
        raise ErrorPlaybook(f"{donde}: no declara ni botones ni comandos a ocultar; no hay nada que hacer")
    vistos = set()
    for j, boton in enumerate(botones):
        aqui = f"{donde}.boton[{j}]"
        exigir_forma(boton, dict, aqui, "boton")
        faltan_b = sorted(set(CLAVES_BOTON) - set(boton))
        sobran_b = sorted(set(boton) - set(CLAVES_BOTON) - set(ACCIONES_BOTON) - set(CLAVES_BOTON_OPCIONALES))
        if faltan_b or sobran_b:
            raise ErrorPlaybook(
                f"{aqui}: un botón exige {{{', '.join(CLAVES_BOTON)}}}, una de {{funcion, menu}}, y admite además "
                f"{{{', '.join(CLAVES_BOTON_OPCIONALES)}}}"
                + (f"; faltan {faltan_b}" if faltan_b else "")
                + (f"; sobran {sobran_b}" if sobran_b else ""))
        acciones_b = [a for a in ACCIONES_BOTON if a in boton]
        if len(acciones_b) != 1:
            raise ErrorPlaybook(
                f"{aqui}: un botón lleva exactamente una de {{funcion, menu}} (funcion: botón común; menu: menú "
                f"desplegable); trae {acciones_b or 'ninguna'}")
        if "regla" in boton:
            exigir_forma(boton["regla"], str, aqui, "regla", no_vacio=True)
        for campo in ("id", "icono"):
            exigir_forma(boton[campo], str, aqui, campo, no_vacio=True)
        if "funcion" in boton:
            exigir_forma(boton["funcion"], str, aqui, "funcion", no_vacio=True)
            for clave in ("idioma_del_texto", "etiquetas_localizadas"):
                if clave in boton:
                    raise ErrorPlaybook(f"{aqui}: '{clave}' es solo para un botón con 'menu'; un botón común usa LocLabels")
        else:
            _validar_menu(boton["menu"], f"{aqui}.menu", idiomas)
            # Un menú declara SIEMPRE cómo lleva su texto, y una sola forma: nunca vuelve a $LocLabels por omisión.
            if "idioma_del_texto" in boton and "etiquetas_localizadas" in boton:
                raise ErrorPlaybook(f"{aqui}: un menú lleva 'etiquetas_localizadas' ($LocLabels, lo recomendado) o "
                                    "'idioma_del_texto' (texto fijo en un idioma), no los dos")
            if "etiquetas_localizadas" in boton:
                if boton["etiquetas_localizadas"] is not True:
                    raise ErrorPlaybook(f"{aqui}: 'etiquetas_localizadas' solo admite true; para texto fijo se usa "
                                        "'idioma_del_texto'")
            elif "idioma_del_texto" not in boton:
                raise ErrorPlaybook(
                    f"{aqui}: un botón con 'menu' declara cómo lleva su texto: 'etiquetas_localizadas': true ($LocLabels, "
                    f"un texto por idioma, lo recomendado) o 'idioma_del_texto' (texto fijo, uno de {idiomas})")
            else:
                exigir_forma(boton["idioma_del_texto"], int, aqui, "idioma_del_texto")
                if boton["idioma_del_texto"] not in idiomas:
                    raise ErrorPlaybook(f"{aqui}: 'idioma_del_texto' = {boton['idioma_del_texto']} no es uno de {idiomas}")
        exigir_forma(boton["secuencia"], int, aqui, "secuencia")
        if boton["id"] in vistos:
            raise ErrorPlaybook(f"{aqui}: el id '{boton['id']}' está repetido en esta tabla")
        vistos.add(boton["id"])
        ubicaciones = exigir_forma(boton["ubicaciones"], [str], aqui, "ubicaciones", no_vacio=True)
        malas = [u for u in ubicaciones if u not in UBICACIONES]
        if malas:
            raise ErrorPlaybook(f"{aqui}: ubicaciones {malas} no válidas; las válidas son {list(UBICACIONES)}")
        for campo in ("etiqueta", "tooltip"):
            _validar_textos(boton[campo], idiomas, f"{aqui}.{campo}")


def _validar_menu(menu, donde, idiomas):
    """Las opciones de un menú desplegable: al menos una, cada una con exactamente sus claves, ids sin repetir."""
    opciones = exigir_forma(menu, [dict], donde, "menu", no_vacio=True)
    vistos = set()
    for k, opcion in enumerate(opciones):
        aqui = f"{donde}[{k}]"
        faltan = sorted(set(CLAVES_OPCION_MENU) - set(opcion))
        sobran = sorted(set(opcion) - set(CLAVES_OPCION_MENU))
        if faltan or sobran:
            raise ErrorPlaybook(
                f"{aqui}: una opción de menú exige exactamente {{{', '.join(CLAVES_OPCION_MENU)}}}"
                + (f"; faltan {faltan}" if faltan else "") + (f"; sobran {sobran}" if sobran else ""))
        for campo in ("id", "funcion"):
            exigir_forma(opcion[campo], str, aqui, campo, no_vacio=True)
        exigir_forma(opcion["secuencia"], int, aqui, "secuencia")
        if opcion["id"] in vistos:
            raise ErrorPlaybook(f"{aqui}: el id de opción '{opcion['id']}' está repetido en este menú")
        vistos.add(opcion["id"])
        for campo in ("etiqueta", "tooltip"):
            _validar_textos(opcion[campo], idiomas, f"{aqui}.{campo}")


def _validar_textos(textos, idiomas, donde):
    """Cada texto del ribbon lleva UNA etiqueta POR IDIOMA: en el XML del
    RibbonDiffXml la plataforma NO sustituye (`01-convenciones.md`)."""
    exigir_forma(textos, dict, donde, "texto")
    faltan = [str(i) for i in idiomas if str(i) not in textos]
    sobran = sorted(set(textos) - {str(i) for i in idiomas})
    if faltan or sobran:
        raise ErrorPlaybook(
            f"{donde}: hace falta un texto por cada idioma {idiomas}; faltan {faltan}, sobran {sobran}")
    for lcid, texto in textos.items():
        exigir_forma(texto, str, donde, f"texto[{lcid}]", no_vacio=True)


# ---------------------------------------------------------------------------
# Edición quirúrgica del customizations.xml
# ---------------------------------------------------------------------------
ABRE_COMPONENTES = "<AppModuleComponents>"
CIERRA_COMPONENTES = "</AppModuleComponents>"


def bloque_componentes(sitemap, tablas, sangria):
    """El bloque `<AppModuleComponents>` tal como lo escribe la plataforma.
    El sitemap va PRIMERO y las tablas en el orden del playbook: así dos
    corridas seguidas generan el mismo texto y la herramienta puede decidir
    que no hay nada que importar."""
    adentro = " " * (len(sangria) + 2)
    filas = [f'{adentro}<AppModuleComponent type="{COMPONENTE_SITEMAP}" schemaName="{sitemap}" />']
    filas += [f'{adentro}<AppModuleComponent type="{COMPONENTE_TABLA}" schemaName="{t}" />' for t in tablas]
    return "\n".join([sangria + ABRE_COMPONENTES] + filas + [sangria + CIERRA_COMPONENTES])


# Grupo donde entra cada botón propio, por ubicación. Verificados contra el
# ribbon real de una tabla propia (`RetrieveEntityRibbon`, 2026-09-22).
GRUPO = {"HomepageGrid": "MainTab.Management", "SubGrid": "MainTab.Management", "Form": "MainTab.Actions"}
# Regla OOB: el botón se habilita con al menos una fila seleccionada.
ENABLE_RULE = "Mscrm.SelectionCountAtLeastOne"
ABRE_RIBBON = "<RibbonDiffXml>"
CIERRA_RIBBON = "</RibbonDiffXml>"


def _base_id(identidad, cambio, boton):
    """`<prefijo>.<abrev>.<tabla sin prefijo>.<boton>`, con el `prefijo` y la `abrev` de la Identidad. El
    prefijo del publisher va primero, como recomienda Learn para garantizar unicidad."""
    corta = cambio["tabla"].split("_")[-1]
    return f"{identidad['prefijo']}.{identidad['abrev']}.{corta}.{boton['id']}"


def _loclabels(base, boton, idiomas):
    """Etiqueta, título y descripción de un botón (o de una opción de menú: `base` es entonces `<base>.<opcion>`)."""
    filas = []
    for sufijo, textos in (("LabelText", boton["etiqueta"]),
                           ("ToolTipTitle", boton["etiqueta"]),
                           ("ToolTipDescription", boton["tooltip"])):
        titulos = "".join(f'<Title description="{_atributo(textos[str(i)])}" languagecode="{i}" />'
                          for i in idiomas)
        filas.append(f'<LocLabel Id="{base}.Button.{sufijo}"><Titles>{titulos}</Titles></LocLabel>')
    return filas


def _atributo(texto):
    """Escapa lo que no puede ir crudo dentro de un atributo XML."""
    return (texto.replace("&", "&amp;").replace("<", "&lt;")
            .replace(">", "&gt;").replace('"', "&quot;"))


def _regla_propia(base, boton, cambio, reglas):
    """La regla de habilitación propia del botón (`regla` del playbook), registrada una vez; devuelve su referencia."""
    if not boton.get("regla"):
        return ""
    reglas.append(
        f'<EnableRule Id="{base}.EnableRule">'
        f'<CustomRule FunctionName="{boton["regla"]}" '
        f'Library="$webresource:{cambio["biblioteca"]}" Default="false">'
        '<CrmParameter Value="SelectedControl" />'
        "</CustomRule></EnableRule>")
    return f'<EnableRule Id="{base}.EnableRule" />'


def _menu_desplegable(base, boton, cambio, idiomas, acciones, comandos, etiquetas, reglas):
    """Un botón con `menu`. Patrón copiado del ribbon REAL de la plataforma
    (`Mscrm.HomepageGrid.<tabla>.Flows.RefreshCommandBar.Flows`, `RetrieveEntityRibbon` en Dev): el ancla es un
    `FlyoutAnchor` con un Command SIN acciones (solo decide si se ve) y `Menu > MenuSection (Menu16) > Controls` con un
    `Button` por opción, cada uno con su Command. A diferencia del botón común, NI el ancla NI las opciones llevan
    `Mscrm.SelectionCountAtLeastOne`: un menú que no actúa sobre filas seleccionadas quedaría deshabilitado, y en
    Unified Interface un comando deshabilitado no se ve. Learn (troubleshoot "Button in command bar doesn't appear
    after grid item selection"): los flyouts se muestran haya o no selección.

    TEXTO: con `etiquetas_localizadas` (lo recomendado) el ancla y cada opción apuntan a sus `$LocLabels` (ids
    `<base>.Button.*` y `<base>.<opcion>.Button.*`), con un texto por idioma, como un botón común. Funciona dentro de un
    FlyoutAnchor: lo probó un experimento controlado (2026-10-02), con la metadata regenerada DESPUÉS de
    importar (un menú localizado mostró su texto en inglés en una interfaz en inglés; el de control, con texto fijo, siguió
    en español). El "LabelText" que había hecho creer lo contrario fue metadata vieja. Con `idioma_del_texto`
    el texto va fijo en ese idioma (Learn, "Use localized labels with ribbons": el ribbon admite texto directo); queda
    para un caso que lo pida, no como regla."""
    propia = _regla_propia(base, boton, cambio, reglas)
    biblioteca = cambio["biblioteca"]
    opciones = sorted(boton["menu"], key=lambda o: o["secuencia"])

    if boton.get("etiquetas_localizadas"):
        etiquetas += _loclabels(base, boton, idiomas)
        for o in opciones:
            etiquetas += _loclabels(f"{base}.{o['id']}", o, idiomas)

        def textos(objeto):
            b = base if objeto is boton else f"{base}.{objeto['id']}"
            return (f'LabelText="$LocLabels:{b}.Button.LabelText" Alt="$LocLabels:{b}.Button.LabelText" '
                    f'ToolTipTitle="$LocLabels:{b}.Button.ToolTipTitle" '
                    f'ToolTipDescription="$LocLabels:{b}.Button.ToolTipDescription"')
    else:
        lcid = str(boton["idioma_del_texto"])

        def textos(objeto):
            etiqueta = _atributo(objeto["etiqueta"][lcid])
            return (f'LabelText="{etiqueta}" Alt="{etiqueta}" ToolTipTitle="{etiqueta}" '
                    f'ToolTipDescription="{_atributo(objeto["tooltip"][lcid])}"')

    for ubicacion in boton["ubicaciones"]:
        lugar = f"Mscrm.{ubicacion}.{cambio['tabla']}.{GRUPO[ubicacion]}.Controls._children"
        botones = "".join(
            f'<Button Command="{base}.{o["id"]}.Command" Id="{base}.{ubicacion}.{o["id"]}.Button" '
            f'Sequence="{o["secuencia"]}" TemplateAlias="o1" {textos(o)} />'
            for o in opciones)
        acciones.append(
            f'<CustomAction Id="{base}.{ubicacion}.CustomAction" Location="{lugar}" '
            f'Sequence="{boton["secuencia"]}"><CommandUIDefinition>'
            f'<FlyoutAnchor Id="{base}.{ubicacion}.FlyoutAnchor" Command="{base}.Command" '
            f'Sequence="{boton["secuencia"]}" TemplateAlias="o1" {textos(boton)} '
            f'Image16by16="$webresource:{boton["icono"]}" Image32by32="$webresource:{boton["icono"]}" '
            f'ModernImage="$webresource:{boton["icono"]}">'
            f'<Menu Id="{base}.{ubicacion}.Menu">'
            f'<MenuSection Id="{base}.{ubicacion}.MenuSection" Sequence="10" DisplayMode="Menu16">'
            f'<Controls Id="{base}.{ubicacion}.Controls">{botones}</Controls>'
            "</MenuSection></Menu></FlyoutAnchor>"
            "</CommandUIDefinition></CustomAction>")
    reglas_ancla = f"<EnableRules>{propia}</EnableRules>" if propia else "<EnableRules />"
    comandos.append(f'<CommandDefinition Id="{base}.Command">{reglas_ancla}<DisplayRules /><Actions /></CommandDefinition>')
    for o in opciones:
        comandos.append(
            f'<CommandDefinition Id="{base}.{o["id"]}.Command">{reglas_ancla}<DisplayRules />'
            f'<Actions><JavaScriptFunction FunctionName="{o["funcion"]}" Library="$webresource:{biblioteca}">'
            '<CrmParameter Value="SelectedControl" />'
            "</JavaScriptFunction></Actions></CommandDefinition>")


def ribbondiffxml(identidad, cambio, botones_presentes):
    """El `<RibbonDiffXml>` COMPLETO de una tabla: los botones propios y los
    genéricos que se ocultan. Los textos salen en los `idiomas` de la Identidad,
    en ese orden (una etiqueta embebida por idioma: la plataforma no sustituye).

    `botones_presentes` son los IDs de botón que EXISTEN en el ribbon real de
    esa tabla (salen de `RetrieveEntityRibbon`). Solo se ocultan esos: un
    `HideCustomAction` sobre un Location inexistente no tiene nada que quitar.

    Se ocultan por el ID del BOTÓN, nunca sobrescribiendo su `CommandDefinition`:
    los comandos OOB son GLOBALES (`Mscrm.DeleteSelectedRecord` y compañía no
    llevan el nombre de la tabla), así que tocarlos apagaría ese botón en TODAS
    las tablas del entorno. El ID del botón sí lleva la tabla."""
    tabla = cambio["tabla"]
    idiomas = identidad["idiomas"]
    acciones, comandos, etiquetas, reglas = [], [], [], []

    for boton in cambio["botones"]:
        base = _base_id(identidad, cambio, boton)
        if "menu" in boton:
            _menu_desplegable(base, boton, cambio, idiomas, acciones, comandos, etiquetas, reglas)
            continue
        for ubicacion in boton["ubicaciones"]:
            lugar = f"Mscrm.{ubicacion}.{tabla}.{GRUPO[ubicacion]}.Controls._children"
            acciones.append(
                f'<CustomAction Id="{base}.{ubicacion}.CustomAction" Location="{lugar}" '
                f'Sequence="{boton["secuencia"]}"><CommandUIDefinition>'
                f'<Button Command="{base}.Command" Id="{base}.{ubicacion}.Button" '
                f'LabelText="$LocLabels:{base}.Button.LabelText" Sequence="{boton["secuencia"]}" '
                f'TemplateAlias="o1" ToolTipTitle="$LocLabels:{base}.Button.ToolTipTitle" '
                f'ToolTipDescription="$LocLabels:{base}.Button.ToolTipDescription" '
                # `ModernImage` es el que MIRA Unified Interface; `Image16by16`/`Image32by32`
                # son del cliente web clásico y ahí quedan por compatibilidad. Sin
                # `ModernImage` la barra de comandos no encuentra icono y pone el genérico
                # de "extensión" (el rompecabezas gris) — que es lo que se veía hasta el
                # 2026-09-23 en los ocho botones, con los SVG correctamente desplegados.
                # Verificado en el ribbon real del entorno: la plataforma usa `ModernImage`
                # 67 veces sobre esta misma tabla, con nombres de su catálogo (`Edit`,
                # `ExportToExcel`) y también con `$webresource:` (`msdyn_approve-email.svg`).
                f'Image16by16="$webresource:{boton["icono"]}" '
                f'Image32by32="$webresource:{boton["icono"]}" '
                f'ModernImage="$webresource:{boton["icono"]}" />'
                "</CommandUIDefinition></CustomAction>")
        # `regla` (opcional): función JS que decide si el botón corresponde para lo que
        # está seleccionado. En Unified Interface un comando deshabilitado NO SE VE
        # (Learn, "Define ribbon enable rules"), así que esto es lo que lo oculta.
        # `Default="false"`: si la regla no llega a evaluarse, el botón no aparece;
        # la función ya devuelve `true` ante la duda, así que las dos capas no se pisan.
        propias = ""
        if boton.get("regla"):
            propias = f'<EnableRule Id="{base}.EnableRule" />'
            reglas.append(
                f'<EnableRule Id="{base}.EnableRule">'
                f'<CustomRule FunctionName="{boton["regla"]}" '
                f'Library="$webresource:{cambio["biblioteca"]}" Default="false">'
                '<CrmParameter Value="SelectedControl" />'
                "</CustomRule></EnableRule>")
        comandos.append(
            f'<CommandDefinition Id="{base}.Command">'
            f'<EnableRules><EnableRule Id="{ENABLE_RULE}" />{propias}</EnableRules>'
            "<DisplayRules />"
            f'<Actions><JavaScriptFunction FunctionName="{boton["funcion"]}" '
            f'Library="$webresource:{cambio["biblioteca"]}">'
            '<CrmParameter Value="SelectedControlSelectedItemIds" />'
            '<CrmParameter Value="SelectedControl" />'
            "</JavaScriptFunction></Actions></CommandDefinition>")
        etiquetas.extend(_loclabels(base, boton, idiomas))

    ocultos = []
    for id_boton in botones_presentes:
        # El punto no vale en un Id de HideCustomAction: se reemplaza por guion.
        limpio = id_boton.replace(".", "-")
        ocultos.append(f'<HideCustomAction HideActionId="{identidad["prefijo"]}.{identidad["abrev"]}.'
                       f'hide.{limpio}.HideAction" Location="{id_boton}" />')

    partes = [ABRE_RIBBON]
    partes.append("<CustomActions>" + "".join(acciones + ocultos) + "</CustomActions>"
                  if (acciones or ocultos) else "<CustomActions />")
    partes.append('<Templates><RibbonTemplates Id="Mscrm.Templates"></RibbonTemplates></Templates>')
    partes.append("<CommandDefinitions>" + "".join(comandos) + "</CommandDefinitions>"
                  if comandos else "<CommandDefinitions />")
    partes.append("<RuleDefinitions><TabDisplayRules /><DisplayRules />"
                  + ("<EnableRules>" + "".join(reglas) + "</EnableRules>" if reglas else "<EnableRules />")
                  + "</RuleDefinitions>")
    partes.append("<LocLabels>" + "".join(etiquetas) + "</LocLabels>" if etiquetas else "<LocLabels />")
    partes.append(CIERRA_RIBBON)
    return "".join(partes)


def _tramo_de_la_entidad(cust, tabla):
    """(inicio, fin) del `<Entity>` cuyo `<Name>` es `tabla`."""
    marca = f">{tabla}</Name>"
    pos = cust.find(marca)
    if pos < 0:
        raise Bloqueado(
            f"la solución exportada no contiene la tabla '{tabla}'; "
            "hay que agregarla a la solución antes de tocarle el ribbon")
    inicio = cust.rfind("<Entity>", 0, pos)
    fin = cust.find("</Entity>", pos)
    if inicio < 0 or fin < 0:
        raise ErrorEntorno(f"el nodo <Entity> de '{tabla}' está mal formado en la solución exportada")
    return inicio, fin


def reemplazar_ribbon(cust, tabla, xml):
    """Devuelve `(xml, hubo_cambio)`. Reemplaza el `<RibbonDiffXml>` entero de
    ESA tabla y deja el resto del archivo como estaba."""
    inicio, fin = _tramo_de_la_entidad(cust, tabla)
    abre = cust.find(ABRE_RIBBON, inicio, fin)
    if abre < 0:
        raise ErrorEntorno(f"la tabla '{tabla}' no tiene nodo <RibbonDiffXml> en la solución exportada")
    cierra = cust.find(CIERRA_RIBBON, abre, fin)
    if cierra < 0:
        raise ErrorEntorno(f"el nodo <RibbonDiffXml> de '{tabla}' no cierra")
    cierra += len(CIERRA_RIBBON)
    viejo = cust[abre:cierra]
    if _normalizar_xml(viejo) == _normalizar_xml(xml):
        return cust, False
    return cust[:abre] + xml + cust[cierra:], True


def _normalizar_xml(texto):
    return "".join((texto or "").split())


def _tramo_del_appmodule(cust, app):
    """(inicio, fin) del `<AppModule>` cuyo `<UniqueName>` es `app`."""
    marca = f"<UniqueName>{app}</UniqueName>"
    pos = cust.find(marca)
    if pos < 0:
        raise Bloqueado(
            f"la solución exportada no contiene ninguna app con UniqueName '{app}'; "
            "hay que crearla primero (`app.py`) y volver a correr esto")
    inicio = cust.rfind("<AppModule>", 0, pos)
    fin = cust.find("</AppModule>", pos)
    if inicio < 0 or fin < 0:
        raise ErrorEntorno(f"el nodo <AppModule> de '{app}' está mal formado en la solución exportada")
    return inicio, fin


def reemplazar_componentes(cust, app, sitemap, tablas):
    """Devuelve `(xml, hubo_cambio)`. No reparsea nada: sustituye el tramo
    exacto del bloque de componentes de ESA app y deja el resto del archivo
    byte por byte como estaba."""
    inicio, fin = _tramo_del_appmodule(cust, app)
    abre = cust.find(ABRE_COMPONENTES, inicio, fin)
    if abre < 0:
        raise Bloqueado(
            f"la app '{app}' no tiene bloque <AppModuleComponents> en la solución exportada; "
            "hay que engancharle el sitemap primero (`app.py`)")
    cierra = cust.find(CIERRA_COMPONENTES, abre, fin)
    if cierra < 0:
        raise ErrorEntorno(f"el bloque <AppModuleComponents> de '{app}' no cierra")
    cierra += len(CIERRA_COMPONENTES)

    # La sangría de la línea donde abre el bloque, para que el archivo siga
    # leyéndose igual que el que produce la plataforma.
    linea = cust.rfind("\n", 0, abre) + 1
    sangria = cust[linea:abre]
    nuevo = bloque_componentes(sitemap, tablas, sangria if sangria.strip() == "" else "")
    viejo = cust[linea if sangria.strip() == "" else abre:cierra]
    if viejo == nuevo:
        return cust, False
    return cust[:linea if sangria.strip() == "" else abre] + nuevo + cust[cierra:], True


def rearmar_zip(zbytes, customizations):
    """Reescribe SOLO `customizations.xml`; los demás archivos del zip viajan
    tal cual, sin recomprimirse mal ni perder su orden."""
    entrada = zipfile.ZipFile(io.BytesIO(zbytes))
    salida_zip = io.BytesIO()
    with zipfile.ZipFile(salida_zip, "w", zipfile.ZIP_DEFLATED) as destino:
        for nombre in entrada.namelist():
            if nombre == "customizations.xml":
                destino.writestr(nombre, customizations.encode("utf-8"))
            else:
                destino.writestr(nombre, entrada.read(nombre))
    return salida_zip.getvalue()


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
def _tablas_de_la_app(dv, id_app):
    cuerpo = leer_entorno(dv, f"RetrieveAppComponents(AppModuleId={id_app})", C_COMPONENTES)
    nombres = set()
    for fila in exigir_forma(cuerpo.get("value"), [dict], C_COMPONENTES, "value"):
        if fila.get("componenttype") != COMPONENTE_TABLA:
            continue
        definicion = leer_entorno(dv, f"EntityDefinitions({fila['objectid']})?$select=LogicalName",
                                  C_COMPONENTES, admite_404=True)
        if definicion and definicion.get("LogicalName"):
            nombres.add(definicion["LogicalName"])
    return nombres


def ribbon_de_la_tabla(dv, tabla):
    """El ribbon REAL de la tabla, ya con las personalizaciones aplicadas.
    `RetrieveEntityRibbon` devuelve un zip en base64 con `RibbonXml.xml`."""
    ruta = (f"RetrieveEntityRibbon(EntityName='{tabla}',"
            "RibbonLocationFilter=Microsoft.Dynamics.CRM.RibbonLocationFilters'All')")
    cuerpo = leer_entorno(dv, ruta, C_RIBBON)
    comprimido = exigir_forma(cuerpo.get("CompressedEntityXml"), str, C_RIBBON,
                              "CompressedEntityXml", no_vacio=True)
    paquete = zipfile.ZipFile(io.BytesIO(base64.b64decode(comprimido)))
    if "RibbonXml.xml" not in paquete.namelist():
        raise ErrorEntorno(f"el ribbon de '{tabla}' no trae RibbonXml.xml")
    return paquete.read("RibbonXml.xml").decode("utf-8")


def todos_los_ids(ribbon_xml):
    """IDs de TODOS los botones del ribbon. Los botones propios NO llevan el
    nombre lógico de la tabla en su id (`<prefijo>.<abrev>.<tabla>.<boton>...`), así que
    verificar que están exige mirar la lista completa, no la filtrada."""
    return sorted(set(re.findall(r'<(?:Button|SplitButton|FlyoutAnchor)[^>]*?\bId="([^"]+)"', ribbon_xml)))


def ids_de_boton(ribbon_xml, tabla):
    """Los botones de la plataforma para ESA tabla, que son los candidatos a
    ocultarse. Se deja afuera la pestaña de gráficos (`.Chart.`): sus botones
    repiten los mismos sufijos y no son los que el diseño manda ocultar."""
    return [i for i in todos_los_ids(ribbon_xml) if f".{tabla}." in i and ".Chart." not in i]


def botones_a_ocultar(ribbon_xml, tabla, sufijos):
    return [i for i in ids_de_boton(ribbon_xml, tabla) if any(i.endswith(s) for s in sufijos)]


def hides_declarados(cust, tabla):
    """Los `Location` de los `HideCustomAction` que la solución YA declara para
    esa tabla."""
    inicio, fin = _tramo_de_la_entidad(cust, tabla)
    return sorted(set(re.findall(r'<HideCustomAction[^>]*?\bLocation="([^"]+)"', cust[inicio:fin])))


def ocultables_de_la_tabla(cust, ribbon_xml, tabla, sufijos):
    """Qué ocultar, uniendo dos fuentes.

    OJO, acá hay una trampa que ya mordió: los botones a ocultar salen del
    ribbon REAL, y un botón que YA está oculto no aparece ahí. Si se mirara
    solo el ribbon, la segunda corrida generaría un RibbonDiffXml sin esos
    `HideCustomAction` y los VOLVERÍA A MOSTRAR. Pasó en Dev el 2026-09-22:
    importar las diez tablas revirtió la tabla que ya estaba hecha.

    Por eso se unen los que se ven ahora con los que la solución ya declara.
    Se filtra por los sufijos del playbook: si el playbook deja de pedir uno,
    ese hide se suelta."""
    visibles = botones_a_ocultar(ribbon_xml, tabla, sufijos)
    previos = [i for i in hides_declarados(cust, tabla) if any(i.endswith(s) for s in sufijos)]
    return sorted(set(visibles) | set(previos))


def _id_de_la_app(dv, app):
    ruta = ("appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()"
            f"?$select=appmoduleid,uniquename&$filter=uniquename eq '{app}'")
    cuerpo = leer_entorno(dv, ruta, C_APP)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_APP, "value")
    if not filas:
        raise Bloqueado(f"la app '{app}' no existe en el entorno; hay que crearla primero (`app.py`)")
    return exigir_forma(filas[0].get("appmoduleid"), str, C_APP, "appmoduleid", no_vacio=True)


def _errores_del_trabajo(dv, id_trabajo):
    """El `importjob` es el único que sabe si la importación hizo algo. Un
    `204` de `ImportSolution` no distingue una importación buena de una que
    entró y falló adentro."""
    cuerpo = leer_entorno(dv, f"importjobs({id_trabajo})?$select=progress,completedon,data",
                          C_TRABAJO, admite_404=True)
    if cuerpo is None:
        return ["la importación no dejó rastro: no existe el importjob"]
    datos = cuerpo.get("data") or ""
    problemas = []
    if 'result="failure"' in datos:
        problemas.append("el trabajo de importación reporta al menos un componente con result=\"failure\"")
    if cuerpo.get("progress") is not None and float(cuerpo["progress"]) < 100:
        problemas.append(f"el trabajo de importación quedó al {cuerpo['progress']}%")
    return problemas


def _verificar_ribbon(dv, cambio, identidad):
    """Qué falta en el ribbon REAL de la tabla: botones propios que no están y
    genéricos que siguen a la vista."""
    tabla = cambio["tabla"]
    ribbon = ribbon_de_la_tabla(dv, tabla)
    presentes = set(todos_los_ids(ribbon))
    faltan = []
    for boton in cambio["botones"]:
        base = _base_id(identidad, cambio, boton)
        for ubicacion in boton["ubicaciones"]:
            if "menu" in boton:
                # Un menú se verifica como se genera (`_menu_desplegable`): el ancla y CADA opción. La primera versión
                # (2026-10-01) buscaba un `Button` donde había un `FlyoutAnchor` y dijo `error` con el ribbon bien hecho.
                if f"{base}.{ubicacion}.FlyoutAnchor" not in presentes:
                    faltan.append(f"falta el menú '{boton['id']}' en {ubicacion}")
                for opcion in boton["menu"]:
                    if f"{base}.{ubicacion}.{opcion['id']}.Button" not in presentes:
                        faltan.append(f"al menú '{boton['id']}' le falta la opción '{opcion['id']}' en {ubicacion}")
            elif f"{base}.{ubicacion}.Button" not in presentes:
                faltan.append(f"falta el botón '{boton['id']}' en {ubicacion}")
        if "menu" in boton and boton.get("etiquetas_localizadas"):
            # RetrieveEntityRibbon devuelve la referencia sin resolver (verificado en Dev el 2026-10-02, botones de Fila).
            for objeto in [boton, *boton["menu"]]:
                b = base if objeto is boton else f"{base}.{objeto['id']}"
                if f'LabelText="$LocLabels:{b}.Button.LabelText"' not in ribbon:
                    faltan.append(f"al menú '{boton['id']}' le falta la etiqueta localizada $LocLabels:{b}.Button.LabelText")
        elif "menu" in boton:
            # El texto fijo también se verifica: con los mismos ids, un menú con el texto viejo pasaría por bueno.
            lcid = str(boton["idioma_del_texto"])
            for objeto in [boton, *boton["menu"]]:
                texto = _atributo(objeto["etiqueta"][lcid])
                if f'LabelText="{texto}"' not in ribbon:
                    faltan.append(f"al menú '{boton['id']}' le falta el texto fijo '{objeto['etiqueta'][lcid]}'")
        for opcion in boton.get("menu", []):
            if f'FunctionName="{opcion["funcion"]}"' not in ribbon:
                faltan.append(f"la opción '{opcion['id']}' del menú '{boton['id']}' no llama a '{opcion['funcion']}'")
        # La regla se verifica aparte porque el botón puede estar y la regla no: el
        # 2026-09-23 se agregaron las cinco EnableRules y esta función dijo
        # `ya_existia` sin mirarlas, así que el cambio no se habría importado nunca.
        # Una verificación más laxa que lo que la herramienta genera es una
        # herramienta que no aplica sus propios cambios.
        if f'ModernImage="$webresource:{boton["icono"]}"' not in ribbon:
            faltan.append(f"al botón '{boton['id']}' le falta el ModernImage (sale sin icono en UCI)")
        if boton.get("regla"):
            if f'"{base}.EnableRule"' not in ribbon:
                faltan.append(f"al botón '{boton['id']}' le falta su EnableRule")
            elif f'FunctionName="{boton["regla"]}"' not in ribbon:
                faltan.append(f"la EnableRule del botón '{boton['id']}' no llama a '{boton['regla']}'")
    siguen = botones_a_ocultar(ribbon, tabla, cambio["ocultar"])
    if siguen:
        faltan.append(f"siguen a la vista {len(siguen)} comandos genéricos: {siguen[:4]}")
    return faltan


def reencender_steps(dv, prefijo, nombres):
    """Vuelve a encender los steps que el `ImportSolution` apagó. Devuelve los que NO pudo.

    **Por qué repara en vez de solo avisar.** Detectar no alcanzaba: el 2026-09-23
    esta guardia disparó TRES veces en una hora, y las tres hubo que leer el
    mensaje y correr un script aparte para reactivar. Un paso manual después de
    una operación que ya falló es un paso que alguien va a saltear, y el costo de
    saltearlo es que la lógica de servidor queda muerta **sin que nada avise**.
    La herramienta sabe exactamente cuáles apagó: los enciende ella.

    Repara SOLO los que ella misma apagó en esta corrida (`nombres` sale de
    comparar antes contra después). Un step que ya estaba apagado se queda
    apagado: puede haber una razón para eso y no es asunto de esta herramienta.
    """
    ruta = ("sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name"
            f"&$filter=contains(name,'{prefijo}')")
    cuerpo = leer_entorno(dv, ruta, C_STEPS)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_STEPS, "value")
    por_nombre = {f["name"]: f["sdkmessageprocessingstepid"] for f in filas}

    fallaron = []
    for nombre in nombres:
        sid = por_nombre.get(nombre)
        if not sid:
            fallaron.append(nombre)
            continue
        est, _, _ = dv.call("PATCH", f"sdkmessageprocessingsteps({sid})",
                            {"statecode": 0, "statuscode": 1})
        if est not in (200, 204):
            fallaron.append(nombre)
    return sorted(fallaron)


def steps_apagados(dv, prefijo):
    """Steps de la solución que quedaron DESHABILITADOS (`statecode = 1`).

    Existe por un daño real: un `ImportSolution` dejó apagados todos los
    steps de la solución, y nadie se enteró hasta el día siguiente.
    El `<SdkMessageProcessingStep>` del `customizations.xml` exportado **no
    lleva `<StateCode>`**, así que al reimportarlo la plataforma los deja
    deshabilitados. Un step apagado no da error: simplemente **no corre**, y la
    lógica de servidor desaparece en silencio.

    Por eso esta herramienta no alcanza con verificar lo que vino a cambiar:
    tiene que comprobar que no rompió otra cosa."""
    ruta = ("sdkmessageprocessingsteps?$select=name,statecode"
            f"&$filter=contains(name,'{prefijo}')")
    cuerpo = leer_entorno(dv, ruta, C_STEPS)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_STEPS, "value")
    return sorted(s["name"] for s in filas if s.get("statecode") == 1)


def tablas_sin_ocultar(dv, datos):
    """Tablas REALES de `datos['app']` (vía `RetrieveAppComponents`, la plataforma,
    nunca lo que el playbook dice que tiene) que este playbook no cubre con un
    cambio `ribbon`. Existe porque una tabla puede entrar a la app y no
    aparecer nunca en el playbook de comandos, y nada lo decía hasta que alguien
    miró la barra real, semanas después. `--solo-verificar` la corre siempre
    que el playbook declare `app` — no depende de que alguien se acuerde de
    mirar."""
    id_app = _id_de_la_app(dv, datos["app"])
    tablas_app = _tablas_de_la_app(dv, id_app)
    con_bloque = {c["tabla"] for c in datos["cambios"] if c["clase"] == "ribbon"}
    excluidas = set(datos.get("tablas_excluidas_de_ocultar", []))
    return sorted(tablas_app - con_bloque - excluidas)


def _contra_entorno(dv, datos, identidad, solo_verificar, componente, forzar=False):
    """Envuelve `_contra_entorno_nucleo` para agregar, SIN tocar ninguno de sus
    caminos de salida, la denuncia de `tablas_sin_ocultar`. Así la lógica de
    exportar/editar/importar (la parte cara y arriesgada) queda intacta, y la
    denuncia es puramente aditiva: un playbook que no declara `app` no cambia
    en nada."""
    estado, comp, detalle = _contra_entorno_nucleo(dv, datos, identidad, solo_verificar, componente, forzar)
    if "app" in datos and estado in ("ya_existia", "difiere", "creado"):
        sin_ocultar = tablas_sin_ocultar(dv, datos)
        if sin_ocultar:
            detalle += " | AVISO: " + "; ".join(
                f"la tabla '{t}' está en la app '{datos['app']}' y no tiene bloque de ocultos" for t in sin_ocultar)
            if estado in ("ya_existia", "creado"):
                # Nada del playbook está mal por sí solo, pero hay una tabla de la
                # app sin cubrir: eso NO es éxito silencioso. Sale del set de
                # códigos 0 de `salida()` a propósito.
                estado = "difiere"
    return estado, comp, detalle


def _contra_entorno_nucleo(dv, datos, identidad, solo_verificar, componente, forzar=False):
    solucion = identidad["solucion"]
    comprobar_solucion_e_idioma(dv, identidad)

    cambios_app = [c for c in datos["cambios"] if c["clase"] == "app_componentes"]
    cambios_ribbon = [c for c in datos["cambios"] if c["clase"] == "ribbon"]
    ids = {c["app"]: _id_de_la_app(dv, c["app"]) for c in cambios_app}

    # --- lo que falta, ANTES de tocar nada ---
    faltantes = {}
    for cambio in cambios_app:
        puestas = _tablas_de_la_app(dv, ids[cambio["app"]])
        faltan = [t for t in cambio["tablas"] if t not in puestas]
        if faltan:
            faltantes[cambio["app"]] = faltan
    for cambio in cambios_ribbon:
        faltan = _verificar_ribbon(dv, cambio, identidad)
        if faltan:
            faltantes[cambio["tabla"]] = faltan
    if not faltantes and not forzar:
        return "ya_existia", componente, (
            "la solución ya tiene todo lo del playbook; no se importó nada "
            f"({sum(len(c['tablas']) for c in cambios_app)} tablas de app y "
            f"{len(cambios_ribbon)} ribbon verificados). "
            "OJO: esta comprobación mira lo que FALTA, no lo que SOBRA — si se sacó algo "
            "de `ocultar`, usar --forzar (ver la nota de `_verificar_ribbon`)")
    if solo_verificar:
        if not faltantes:
            return "ya_existia", componente, "no falta nada (no se comprobó lo que sobra)"
        detalle = "; ".join(f"a '{a}' le faltan {t}" for a, t in faltantes.items())
        return "difiere", componente, detalle

    # Foto del estado de los steps ANTES de tocar nada: si el import apaga
    # alguno que estaba encendido, hay que verlo.
    # Lo que la herramienta ARREGLÓ sola. No es un problema, pero tiene que salir en
    # la salida: un daño reparado en silencio es un daño que nadie va a investigar.
    reparados = []
    apagados_antes = set(steps_apagados(dv, identidad["abrev"].upper()))

    # --- exportar ---
    est, resp, _ = dv.call("POST", "ExportSolution",
                           {"SolutionName": solucion, "Managed": False}, timeout=600)
    if est != 200 or not resp.get("ExportSolutionFile"):
        return "error", componente, f"ExportSolution devolvió HTTP {est}: {str(resp)[:300]}"
    zbytes = base64.b64decode(resp["ExportSolutionFile"])
    entrada = zipfile.ZipFile(io.BytesIO(zbytes))
    if "customizations.xml" not in entrada.namelist():
        return "error", componente, "la solución exportada no trae customizations.xml"
    cust = entrada.read("customizations.xml").decode("utf-8")

    # --- editar ---
    tocadas, ribbons = [], []
    for cambio in cambios_app:
        cust, hubo = reemplazar_componentes(cust, cambio["app"], cambio["sitemap"], cambio["tablas"])
        if hubo:
            tocadas.append(cambio["app"])
    for cambio in cambios_ribbon:
        # Se ocultan los botones que existen de verdad en esa tabla MÁS los que
        # la solución ya declara ocultos: mirar solo el ribbon los des-ocultaría.
        ocultables = ocultables_de_la_tabla(cust, ribbon_de_la_tabla(dv, cambio["tabla"]),
                                            cambio["tabla"], cambio["ocultar"])
        xml = ribbondiffxml(identidad, cambio, ocultables)
        cust, hubo = reemplazar_ribbon(cust, cambio["tabla"], xml)
        if hubo:
            ribbons.append(cambio["tabla"])
    if not tocadas and not ribbons:
        return "error", componente, (
            "el entorno dice que faltan componentes pero el customizations.xml exportado ya los "
            "tiene: la solución exportada y el entorno no coinciden, no se importa nada")

    # --- importar ---
    id_trabajo = str(uuid.uuid4())
    est, resp, _ = dv.call("POST", "ImportSolution", {
        "OverwriteUnmanagedCustomizations": True,
        "PublishWorkflows": False,
        "CustomizationFile": base64.b64encode(rearmar_zip(zbytes, cust)).decode("ascii"),
        "ImportJobId": id_trabajo,
    }, timeout=900)
    if est not in (200, 204):
        return "error", componente, f"ImportSolution devolvió HTTP {est}: {str(resp)[:300]}"

    problemas = _errores_del_trabajo(dv, id_trabajo)

    # --- publicar cada app tocada ---
    for app in tocadas:
        xml = (f"<importexportxml><appmodules><appmodule>{ids[app]}</appmodule></appmodules>"
               "</importexportxml>")
        est, resp, _ = dv.call("POST", "PublishXml", {"ParameterXml": xml}, timeout=600)
        if est not in (200, 204):
            problemas.append(f"la publicación de '{app}' devolvió HTTP {est}: {str(resp)[:200]}")

    # Un cambio de ribbon no se ve hasta publicar la tabla.
    if ribbons:
        entidades = "".join(f"<entity>{t}</entity>" for t in ribbons)
        est, resp, _ = dv.call("POST", "PublishXml",
                               {"ParameterXml": f"<importexportxml><entities>{entidades}</entities>"
                                                "</importexportxml>"}, timeout=900)
        if est not in (200, 204):
            problemas.append(f"la publicación de las tablas devolvió HTTP {est}: {str(resp)[:200]}")

    # --- ¿el import apagó algún step que estaba encendido? ---
    apagados_ahora = set(steps_apagados(dv, identidad["abrev"].upper()))
    rotos = sorted(apagados_ahora - apagados_antes)
    if rotos:
        fallaron = reencender_steps(dv, identidad["abrev"].upper(), rotos)
        if fallaron:
            problemas.append(
                f"el import DESHABILITÓ {len(rotos)} step(s) y a {len(fallaron)} NO SE PUDO VOLVER A "
                f"ENCENDER: {fallaron[:6]}" + (" …" if len(fallaron) > 6 else "")
                + ". Un step apagado no da error, simplemente no corre: hay que encenderlos a mano")
        else:
            reparados.append(
                f"el import deshabilitó {len(rotos)} step(s) y se volvió a encender todos: {rotos[:6]}"
                + (" …" if len(rotos) > 6 else ""))

    # --- verificar lo que QUEDÓ ---
    for cambio in cambios_app:
        puestas = _tablas_de_la_app(dv, ids[cambio["app"]])
        faltan = [t for t in cambio["tablas"] if t not in puestas]
        if faltan:
            problemas.append(f"después de importar, a '{cambio['app']}' le siguen faltando {faltan}")
    for cambio in cambios_ribbon:
        faltan = _verificar_ribbon(dv, cambio, identidad)
        if faltan:
            problemas.append(f"después de importar, el ribbon de '{cambio['tabla']}': {'; '.join(faltan)}")

    if problemas:
        return "error", componente, "se importó pero no quedó bien: " + "; ".join(problemas)

    total = sum(len(c["tablas"]) for c in cambios_app)
    botones = sum(len(c["botones"]) for c in cambios_ribbon)
    return "creado", componente, (
        f"importada la solución '{solucion}' (importjob {id_trabajo}); "
        f"{len(tocadas)} app(s) con {total} tablas, {len(ribbons)} ribbon con {botones} botones propios; "
        "todo verificado contra el entorno"
        + ("; " + "; ".join(reparados) if reparados else "")
        + (AVISO_REGENERAR if ribbons else ""))


def construir(ruta_playbook, solo_verificar, fabrica_cliente, dormir=None, forzar=False):
    """Nunca lanza: siempre devuelve `(estado, componente, detalle)`."""
    componente = os.path.basename(ruta_playbook)
    paso = "leer el playbook"
    try:
        secciones = dividir_secciones(leer_texto(ruta_playbook))
        paso = "leer el bloque '## 2. Qué se crea'"
        datos = obtener_componente(secciones, TIPO)
        paso = "leer el bloque '## 1. Identidad'"
        identidad = obtener_identidad(secciones)
        if isinstance(identidad.get("solucion"), str):
            componente = identidad["solucion"]
        paso = "validar el bloque de la importación"
        validar_playbook(datos, identidad)
    except ErrorPlaybook as e:
        return "error", componente, str(e)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except OSError as e:
        return "error", componente, f"no se pudo leer el playbook '{ruta_playbook}': {e}"
    except Exception as e:
        return "error", componente, f"fallo inesperado validando el playbook (offline), al {paso}: {type(e).__name__}: {e}"

    print(f"Playbook leído y validado (offline). tipo={TIPO} solucion={componente}")
    try:
        dv = fabrica_cliente()
    except Exception:
        return "error", componente, MENSAJE_FALLO_CLIENTE

    rastro = Rastro(dv)
    try:
        return _contra_entorno(rastro, datos, identidad, solo_verificar, componente, forzar)
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
    desconocidas = sorted(banderas - {"--solo-verificar", "--forzar"})
    if len(rutas) != 1 or desconocidas:
        return salida(
            "error", "desconocido",
            "uso incorrecto: importar_solucion.py <playbook.md> [--solo-verificar] [--forzar]"
            + (f"; banderas desconocidas: {desconocidas}" if desconocidas else ""),
        )
    from dataverse_api import Dataverse

    estado, componente, detalle = construir(rutas[0], "--solo-verificar" in banderas, Dataverse,
                                           forzar="--forzar" in banderas)
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
