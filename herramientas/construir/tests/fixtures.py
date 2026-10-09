"""Datos y helpers compartidos por las pruebas de `herramientas/construir/`."""
import copy
import json
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
_CONSTRUIR = os.path.dirname(_AQUI)
if _CONSTRUIR not in sys.path:
    sys.path.insert(0, _CONSTRUIR)

IDIOMAS = [1033, 3082]

IDENTIDAD_VALIDA = {
    "tipo_playbook": "choice-global",
    "version_skill": "0.2.0",
    "proyecto": "2026-003-control-arreglos-pago",
    "inventario": "2.1",
    "fase": 1,
    "entorno_url": "https://ensayo.crm.dynamics.com/",
    "solucion": "sanic_carp_sol_controlarreglospago",
    "publisher": "Sistemas_Abiertos_Nicaragua",
    "prefijo": "sanic",
    "abrev": "carp",
    "prefijo_opciones": 15946,
    "idiomas": [1033, 3082],
}


def por_idioma(texto, idiomas=None):
    """El mismo texto en cada idioma, como lo escribe un playbook: `{"1033": t, "3082": t}`."""
    return {str(i): texto for i in (IDIOMAS if idiomas is None else idiomas)}


# Dominio de este proyecto (diccionario de datos, sección 3.1): choice global de moneda del arreglo.
COMPONENTE_VALIDO = {
    "tipo": "choice-global",
    "nombre": "sanic_carp_ch_moneda",
    "displayname": por_idioma("CH - CARP - Moneda"),
    "descripcion": "Moneda en la que se expresan los montos de un arreglo y de sus cuotas: córdobas o dólares.",
    "opciones": [
        {"valor": 159460001, "etiqueta": por_idioma("Cordobas"), "descripcion": "Córdobas nicaragüenses"},
        {"valor": 159460002, "etiqueta": por_idioma("Dolares"), "descripcion": "Dólares estadounidenses"},
    ],
}


def identidad(**cambios):
    d = copy.deepcopy(IDENTIDAD_VALIDA)
    d.update(cambios)
    return d


def componente(**cambios):
    d = copy.deepcopy(COMPONENTE_VALIDO)
    d.update(cambios)
    return d


def playbook_md(identidad_dict=None, componente_dict=None, con_seccion_1=True, con_seccion_2=True, bloques_extra_seccion_2=0):
    """Arma el texto mínimo de un playbook: solo las secciones 1 y 2 (las
    únicas que el código lee). `bloques_extra_seccion_2` permite meter más de
    un bloque ```json``` en la sección 2, para probar el rechazo."""
    identidad_dict = IDENTIDAD_VALIDA if identidad_dict is None else identidad_dict
    componente_dict = COMPONENTE_VALIDO if componente_dict is None else componente_dict
    partes = ["# Playbook: choice-global · prueba\n"]
    if con_seccion_1:
        partes.append("## 1. Identidad\n\n```json\n" + json.dumps(identidad_dict, ensure_ascii=False, indent=2) + "\n```\n")
    if con_seccion_2:
        bloque2 = "## 2. Qué se crea\n\n```json\n" + json.dumps(componente_dict, ensure_ascii=False, indent=2) + "\n```\n"
        for _ in range(bloques_extra_seccion_2):
            bloque2 += "\n```json\n" + json.dumps(componente_dict, ensure_ascii=False, indent=2) + "\n```\n"
        partes.append(bloque2)
    partes.append("## 3. Precondiciones\n\n(no hace falta para las pruebas)\n")
    return "\n".join(partes)


def escribir_playbook(ruta, identidad_dict=None, componente_dict=None, **kw):
    with open(ruta, "w", encoding="utf-8") as f:
        f.write(playbook_md(identidad_dict, componente_dict, **kw))


def label(texto, idiomas=None, otros_idiomas=()):
    """Construye un `Label` de la forma que devuelve el Web API. `texto` es un
    texto (el mismo en cada idioma de `idiomas`, por defecto los del proyecto)
    o un `{"1033": ..., "3082": ...}`. `otros_idiomas` agrega etiquetas de MÁS
    (para probar el rechazo de 'etiquetas en un idioma que el playbook no declara')."""
    idiomas = IDIOMAS if idiomas is None else idiomas
    if isinstance(texto, dict):
        locs = [{"Label": texto[str(i)], "LanguageCode": i} for i in idiomas if str(i) in texto]
    else:
        locs = [{"Label": texto, "LanguageCode": i} for i in idiomas]
    for lc, txt in otros_idiomas:
        locs.append({"Label": txt, "LanguageCode": lc})
    return {"LocalizedLabels": locs}


def cuerpo_choice_conforme(datos=None, idiomas=None, is_managed=False, metadata_id="11111111-1111-1111-1111-111111111111"):
    """El cuerpo que devolvería `GET GlobalOptionSetDefinitions(Name=...)`
    si el entorno tuviera exactamente lo que pide `datos` (COMPONENTE_VALIDO
    por default)."""
    datos = COMPONENTE_VALIDO if datos is None else datos
    return {
        "Name": datos["nombre"],
        "MetadataId": metadata_id,
        "IsGlobal": True,
        "OptionSetType": "Picklist",
        "IsManaged": is_managed,
        "DisplayName": label(datos["displayname"], idiomas),
        "Description": label(datos["descripcion"], idiomas),
        "Options": [
            {
                "Value": op["valor"],
                "Label": label(op["etiqueta"], idiomas),
                "Description": label(op.get("descripcion") or "", idiomas),
            }
            for op in datos["opciones"]
        ],
    }


SOLUTION_ID = "22222222-2222-2222-2222-222222222222"


def respuesta_solucion_ok(identidad_dict=None):
    identidad_dict = IDENTIDAD_VALIDA if identidad_dict is None else identidad_dict
    return (
        200,
        {
            "value": [
                {
                    "uniquename": identidad_dict["solucion"],
                    "solutionid": SOLUTION_ID,
                    "ismanaged": False,
                    "publisherid": {
                        "uniquename": identidad_dict["publisher"],
                        "customizationprefix": identidad_dict["prefijo"],
                        "customizationoptionvalueprefix": identidad_dict["prefijo_opciones"],
                    },
                }
            ]
        },
        {},
    )


def respuesta_idioma_ok(identidad_dict=None, base=1033):
    """Idioma base del entorno (`organizations.languagecode`). El de Dev es 1033."""
    return (200, {"value": [{"languagecode": base}]}, {})


def respuesta_idiomas_provisionados_ok(identidad_dict=None):
    """Los idiomas provisionados son los del playbook (si no, los del proyecto)."""
    identidad_dict = IDENTIDAD_VALIDA if identidad_dict is None else identidad_dict
    return (200, {"RetrieveProvisionedLanguages": list(identidad_dict["idiomas"])}, {})


def ruta_choice(nombre):
    return f"GlobalOptionSetDefinitions(Name='{nombre}')"


def es_ruta_solutioncomponents(ruta):
    return ruta.startswith("solutioncomponents?")


def es_ruta_solutions(ruta):
    return ruta.startswith("solutions?")


def armar_cliente_precondiciones_ok(cliente, identidad_dict=None):
    """Configura en `cliente` las tres respuestas de precondición felices
    (solución/publisher, idioma) para que una prueba solo tenga que agregar
    la respuesta de existencia del choice."""
    identidad_dict = IDENTIDAD_VALIDA if identidad_dict is None else identidad_dict
    cliente.responder("GET", es_ruta_solutions, respuesta_solucion_ok(identidad_dict))
    cliente.responder("GET", "organizations?$select=languagecode", respuesta_idioma_ok(identidad_dict))
    cliente.responder("GET", "RetrieveProvisionedLanguages", respuesta_idiomas_provisionados_ok(identidad_dict))
    return cliente
