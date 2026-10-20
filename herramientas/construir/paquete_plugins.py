#!/usr/bin/env python3
"""Registra UN paquete de plugins (.nupkg) en Dataverse, desde un playbook de
tipo `paquete-plugins`. Contrato: `power-platform-construir`,
`references/dataverse/alm.md`; Learn, "Build and package plug-in code".

    python3 herramientas/construir/paquete_plugins.py <playbook.md> [--solo-verificar] [--actualizar-contenido]

Existe porque `pac` y la Plug-in Registration Tool no sirven en una sesión sin
escritorio, y porque un `pluginassembly` suelto NO admite ensamblados
dependientes: un plugin que usa una librería de terceros va en paquete.

Lo que hace la plataforma sola, y esta herramienta comprueba: al subir el
.nupkg, Dataverse registra en `pluginassembly` los ensamblados que contienen
clases `IPlugin`, y en `plugintype` cada una de esas clases. Si un tipo
esperado no aparece, el paquete se subió pero NO sirve: los steps y las
Custom API no van a tener a qué apuntar. Por eso `tipos` es obligatorio en el
playbook y se verifica uno por uno.

Nunca borra. `name` y `version` son INMUTABLES en el servidor (Learn): si el
paquete existe con otros valores, informa `difiere` y para — se resuelve
borrando a mano, que se lleva puestos ensamblados, tipos y TODOS sus steps.
Lo único que se puede actualizar es el contenido, y solo si se lo piden con
`--actualizar-contenido`.

Verificación del binario (antes de escribir en el entorno, cuando se lee el
.nupkg: en el alta y con `--actualizar-contenido`):

  - el `.nuspec` de adentro dice `id` = `uniquename` y `version` = `version`;
  - cada dependencia del nuspec que no es de plataforma (Microsoft.CrmSdk.*,
    Microsoft.Xrm.*, herramientas de compilación) viene como .dll en lib/net462;
  - los `tipos` están definidos, con namespace completo, en algún .dll de
    lib/net462 (se lee la tabla TypeDef de la metadata: `_metadata_net.py`).

Verificación del entorno (los tres caminos): el `version` de cada ensamblado
del paquete es `version_ensamblado` (el AssemblyVersion). Con
`--actualizar-contenido` se leen `version` y `modifiedon` antes y después del
PATCH: un 204 no prueba que el sandbox ejecute el binario nuevo, y si
`modifiedon` no avanzó es `error`.

Última línea de la salida: JSON de una línea con `estado`
(creado | actualizado | ya_existia | difiere | bloqueado | error), `componente`, `detalle`.
"""
import base64
import datetime
import io
import os
import sys
import xml.etree.ElementTree as ElementTree
import zipfile

_AQUI = os.path.dirname(os.path.abspath(__file__))
_RAIZ = os.path.dirname(os.path.dirname(_AQUI))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

from _metadata_net import NoEsEnsambladoNet, nombres_de_tipos  # noqa: E402
from _comun import (  # noqa: E402
    Bloqueado,
    ErrorEntorno,
    ErrorPlaybook,
    Rastro,
    comprobar_solucion_e_idioma,
    dividir_secciones,
    escribir_metadatos,
    exigir_forma,
    exigir_sin_tildes,
    fuera_de_la_solucion,
    leer_entorno,
    leer_texto,
    obtener_componente,
    obtener_identidad,
    salida,
)

TIPO = "paquete-plugins"

# Largos de columna del Web API (Learn, tabla PluginPackage, verificado 2026-09-22).
LARGO_NOMBRE = 100
LARGO_UNIQUENAME = 128
LARGO_VERSION = 100

# Tope propio, no de la plataforma: un .nupkg más grande que esto es señal de
# que se coló el SDK de Dataverse o algo que ya vive en el sandbox. El límite
# real de un ensamblado es 16 MB (Learn), pero acá queremos enterarnos ANTES.
MAXIMO_BYTES_NUPKG = 12 * 1024 * 1024

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)

CLAVES = {
    "tipo": str, "nombre": str, "uniquename": str, "version": str, "version_ensamblado": str, "nupkg": str, "tipos": list,
}

# El sandbox carga los ensamblados de esta carpeta del paquete y de ninguna otra.
CARPETA_DEL_SANDBOX = "lib/net462/"

# Dependencias que el sandbox ya trae (el SDK de Dataverse) o que son herramientas de compilación y no viajan al
# runtime (`Microsoft.PowerApps.MSBuild.Plugin` figura en el nuspec de un paquete real con `exclude="Build,Analyzers"`).
# Se comparan en minúsculas y con el punto final: `Microsoft.XrmToolkit` no es del SDK.
PREFIJOS_DE_PLATAFORMA = ("microsoft.crmsdk.", "microsoft.xrm.", "microsoft.powerapps.msbuild.")


class ErrorNupkg(Exception):
    """El .nupkg no es el paquete que el playbook declara (identidad, versión o dependencias).
    Nunca se llegó a escribir en el entorno; termina en el estado `error`."""

C_PAQUETE = "la consulta del paquete de plugins (GET pluginpackages)"
C_ENSAMBLADOS = "la consulta de los ensamblados del paquete (GET pluginassemblies)"
C_TIPOS = "la consulta de los tipos de plugin (GET plugintypes)"
C_COMPONENTES = "la consulta de pertenencia a la solución (GET solutioncomponents)"


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def _caracteres_de_identificador(texto):
    """Lo que Dataverse admite en un unique name: letras y dígitos ASCII, guión
    bajo y punto. Se comprueba por rango de caracteres, no con una expresión
    regular, para que no dependa de la cultura ni de la localización."""
    return all(("a" <= c <= "z") or ("A" <= c <= "Z") or ("0" <= c <= "9") or c in "_." for c in texto)


def _version_bien_formada(texto):
    """Dos a cuatro números separados por punto, sin ceros a la izquierda de más
    de un dígito. NuGet acepta más formas; acá se exige la que produce el
    proyecto de empaquetado, porque `version` no se puede corregir después."""
    partes = texto.split(".")
    if not 2 <= len(partes) <= 4:
        return False
    for p in partes:
        if not p or not all("0" <= c <= "9" for c in p):
            return False
        if len(p) > 1 and p[0] == "0":
            return False
    return True


def _validar_estructura(datos, identidad):
    faltan, sobran = sorted(set(CLAVES) - set(datos)), sorted(set(datos) - set(CLAVES))
    if faltan or sobran:
        partes = []
        if faltan:
            partes.append(f"faltan {faltan}")
        if sobran:
            partes.append(f"sobran {sobran}")
        raise ErrorPlaybook(f"el bloque del paquete no tiene las claves esperadas: {'; '.join(partes)}")
    for clave, tipo in CLAVES.items():
        exigir_forma(datos[clave], tipo, "el bloque del paquete", clave, no_vacio=True)

    if datos["tipo"] != TIPO:
        raise ErrorPlaybook(f"el bloque del paquete dice tipo = {datos['tipo']!r}, se esperaba {TIPO!r}")

    unico = datos["uniquename"]
    if len(unico) > LARGO_UNIQUENAME:
        raise ErrorPlaybook(f"uniquename tiene {len(unico)} caracteres; el máximo de la columna es {LARGO_UNIQUENAME}")
    if not _caracteres_de_identificador(unico):
        raise ErrorPlaybook(f"uniquename = {unico!r} solo puede llevar letras y dígitos ASCII, guión bajo y punto")
    # Convención del estudio, no de la plataforma: Learn no exige prefijo en el
    # unique name de un paquete (sí en el de una Custom API). Se exige igual
    # porque en un entorno compartido por varias soluciones un nombre sin el
    # prefijo del publisher invita a la colisión. El prefijo sale de la Identidad.
    prefijo = identidad["prefijo"] + "_"
    if not unico.startswith(prefijo):
        raise ErrorPlaybook(f"uniquename = {unico!r} no empieza con el prefijo del publisher {prefijo!r}")

    exigir_sin_tildes(datos["nombre"], "nombre")
    if len(datos["nombre"]) > LARGO_NOMBRE:
        raise ErrorPlaybook(f"nombre tiene {len(datos['nombre'])} caracteres; el máximo de la columna es {LARGO_NOMBRE}")
    # La plataforma rechazó con 400 un `name` en texto libre en los spikes 2026-003 (los paquetes que sí se
    # registraron llevan `name` = `uniquename`): se frena acá, antes de intentarlo.
    if datos["nombre"] != unico:
        raise ErrorPlaybook(
            f"nombre = {datos['nombre']!r} tiene que ser igual a uniquename = {unico!r}: la plataforma rechaza un name libre"
        )

    if not _version_bien_formada(datos["version"]):
        raise ErrorPlaybook(
            f"version = {datos['version']!r} no tiene la forma esperada (de dos a cuatro números separados por punto); "
            "y `version` NO se puede cambiar en el servidor una vez creado el paquete"
        )
    if len(datos["version"]) > LARGO_VERSION:
        raise ErrorPlaybook(f"version tiene {len(datos['version'])} caracteres; el máximo de la columna es {LARGO_VERSION}")

    # `pluginassemblies.version` es el AssemblyVersion del ensamblado: cuatro números. No es la versión del paquete.
    if not _version_bien_formada(datos["version_ensamblado"]) or datos["version_ensamblado"].count(".") != 3:
        raise ErrorPlaybook(
            f"version_ensamblado = {datos['version_ensamblado']!r} no tiene la forma esperada: cuatro números separados por "
            "punto (el AssemblyVersion, por ejemplo '1.0.0.0'), sin ceros a la izquierda"
        )

    ruta = datos["nupkg"]
    if not ruta.endswith(".nupkg"):
        raise ErrorPlaybook(f"nupkg = {ruta!r} no termina en '.nupkg'")
    if os.path.isabs(ruta) or ".." in ruta.split("/"):
        raise ErrorPlaybook(f"nupkg = {ruta!r} tiene que ser una ruta relativa a la raíz del repositorio, sin '..'")

    tipos = datos["tipos"]
    for i, t in enumerate(tipos):
        exigir_forma(t, str, "el bloque del paquete", f"tipos[{i}]", no_vacio=True)
        if "." not in t:
            raise ErrorPlaybook(f"tipos[{i}] = {t!r} no parece un nombre de tipo .NET completo (namespace + clase)")
    if len(set(tipos)) != len(tipos):
        repetidos = sorted({t for t in tipos if tipos.count(t) > 1})
        raise ErrorPlaybook(f"tipos tiene entradas repetidas: {repetidos}")


def _como_error_de_playbook(e):
    """`exigir_forma` habla de "una respuesta del entorno" (`devolvió 200 con forma inesperada`) y lanza
    `ErrorEntorno`; acá el dato mal formado es del PLAYBOOK y se informa como tal, no como un fallo imprevisto."""
    donde, separador, resto = str(e).partition(" devolvió 200 con forma inesperada: ")
    return f"{donde}: {resto}" if separador else str(e)


def validar_playbook(datos, identidad):
    try:
        _validar_estructura(datos, identidad)
    except ErrorEntorno as e:
        raise ErrorPlaybook(_como_error_de_playbook(e)) from None


def _abrir_nupkg(crudo):
    try:
        return zipfile.ZipFile(io.BytesIO(crudo))
    except zipfile.BadZipFile:
        raise Bloqueado(
            "el .nupkg no es un .zip válido; es la señal de que `dotnet pack` no lo regeneró "
            "(MSBuild puede dar `GenerateNuspec` por \"up-to-date\" contra una caché de `obj/` vieja: "
            "borrar `obj/` y `bin/` a mano y volver a empaquetar)"
        ) from None


def _dlls_del_sandbox(paquete):
    """Los `.dll` que están DIRECTAMENTE en `lib/net462/`: la única carpeta que el sandbox carga. Un .dll en otro
    destino (`lib/netstandard2.0/`) o en una subcarpeta no le sirve, aunque tenga los tipos."""
    dlls = []
    for nombre in paquete.namelist():
        resto = nombre[len(CARPETA_DEL_SANDBOX):]
        if nombre.lower().startswith(CARPETA_DEL_SANDBOX) and resto and "/" not in resto and resto.lower().endswith(".dll"):
            dlls.append(nombre)
    return dlls


def _sin_espacio_de_nombres(etiqueta):
    return etiqueta.rsplit("}", 1)[-1]


def _hijo(elemento, nombre):
    for h in elemento:
        if _sin_espacio_de_nombres(h.tag) == nombre:
            return h
    return None


def _leer_nuspec(paquete):
    """(id, version, ids de dependencias) del `.nuspec` de la raíz del paquete. Lanza `ErrorNupkg` si falta, no es
    XML, declara un DOCTYPE (no se expande: nada de entidades) o no dice `id` o `version`."""
    nuspecs = sorted(n for n in paquete.namelist() if "/" not in n and n.lower().endswith(".nuspec"))
    if not nuspecs:
        raise ErrorNupkg("el .nupkg no trae ningún .nuspec en su raíz; no se puede comprobar qué paquete es")
    crudo = paquete.read(nuspecs[0])
    minusculas = crudo.lower()
    if b"<!doctype" in minusculas or b"<!entity" in minusculas:
        raise ErrorNupkg(f"el nuspec '{nuspecs[0]}' declara un DOCTYPE o entidades; no se lee (un nuspec de `dotnet pack` no los lleva)")
    try:
        raiz = ElementTree.fromstring(crudo)
    except ElementTree.ParseError as e:
        raise ErrorNupkg(f"el nuspec '{nuspecs[0]}' no es XML válido: {e}") from None
    metadata = _hijo(raiz, "metadata")
    if metadata is None:
        raise ErrorNupkg(f"el nuspec '{nuspecs[0]}' no tiene <metadata>")
    valores = {}
    for clave in ("id", "version"):
        elemento = _hijo(metadata, clave)
        texto = (elemento.text or "").strip() if elemento is not None else ""
        if not texto:
            raise ErrorNupkg(f"el nuspec '{nuspecs[0]}' no declara <{clave}>")
        valores[clave] = texto
    dependencias = {
        d.get("id", "").strip()
        for d in metadata.iter()
        if _sin_espacio_de_nombres(d.tag) == "dependency" and d.get("id", "").strip()
    }
    return valores["id"], valores["version"], dependencias


def _es_de_plataforma(id_dependencia):
    return id_dependencia.lower().startswith(PREFIJOS_DE_PLATAFORMA)


def _exigir_el_paquete_declarado(paquete, datos):
    """Lo que el .nupkg dice de sí mismo tiene que ser lo que el playbook declara, y todo lo que el nuspec pide
    tiene que venir adentro: el sandbox no resuelve paquetes de NuGet, solo carga los .dll del paquete. Se compara
    el id y la versión EXACTOS (sin normalizar mayúsculas): el unique name es el que la plataforma va a registrar."""
    id_nuspec, version_nuspec, dependencias = _leer_nuspec(paquete)
    problemas = []
    if id_nuspec != datos["uniquename"]:
        problemas.append(
            f"el nuspec dice id = {id_nuspec!r} y el playbook dice uniquename = {datos['uniquename']!r} "
            "(revisar PackageId del proyecto de empaquetado)"
        )
    if version_nuspec != datos["version"]:
        problemas.append(
            f"el nuspec dice version = {version_nuspec!r} y el playbook dice version = {datos['version']!r} "
            "(revisar Version del proyecto de empaquetado; la del paquete es inmutable en el servidor)"
        )
    if problemas:
        raise ErrorNupkg("el .nupkg no es el paquete que el playbook declara: " + "; ".join(problemas))

    presentes = {n.rsplit("/", 1)[-1][:-4].lower() for n in _dlls_del_sandbox(paquete)}
    faltan = sorted(d for d in dependencias if not _es_de_plataforma(d) and d.lower() not in presentes)
    if faltan:
        raise ErrorNupkg(
            f"el nuspec declara dependencias que no vienen como .dll en {CARPETA_DEL_SANDBOX} dentro del paquete: {faltan}. "
            "El sandbox no resuelve paquetes de NuGet: sin su .dll el plugin falla al cargar. Revisar que el proyecto de "
            "empaquetado las copie a la salida (y que no estén marcadas PrivateAssets)"
        )


def _tipos_faltantes_en_dll(crudo, tipos_declarados):
    """Abre el .nupkg como el .zip que es, lee la metadata (tabla `TypeDef`) de
    cada ensamblado .NET que esté en `lib/net462/` y compara el nombre COMPLETO
    de cada tipo declarado (namespace + clase). Buscar solo el nombre de la clase
    daba por bueno `Otro.Namespace.Clase`; ver `_metadata_net.py`.

    Devuelve la lista de tipos declarados que NO aparecen. Lanza `Bloqueado`
    si el archivo no es un .zip válido o si no trae ningún .dll en esa carpeta: en los dos
    casos no tiene sentido seguir, porque no hay contenido que comparar. Un .dll
    que no es un ensamblado .NET (nativo, corrupto) no aporta tipos y se ignora.

    Por qué por CONTENIDO y no por fecha: MSBuild puede
    dar `GenerateNuspec` por "up-to-date" contra una caché de `obj/`
    desactualizada, y entonces `dotnet pack` termina con éxito sin regenerar
    el `.nupkg` de verdad. `dotnet clean` no lo arregla porque `Pack` no es
    parte de ese target. Eso hizo que se subiera un paquete sin las clases
    nuevas y se midiera código viejo durante un día entero. Una fecha dice que
    el archivo se escribió; solo el contenido dice qué tiene adentro."""
    with _abrir_nupkg(crudo) as paquete:
        dlls = _dlls_del_sandbox(paquete)
        if not dlls:
            raise Bloqueado(
                f"el .nupkg no trae ningún .dll en '{CARPETA_DEL_SANDBOX}'; no se puede verificar que traiga los tipos "
                "que el playbook declara (el sandbox carga esa carpeta y ninguna otra)"
            )
        definidos = set()
        for nombre in dlls:
            try:
                definidos |= nombres_de_tipos(paquete.read(nombre))
            except NoEsEnsambladoNet:
                continue
    return [t for t in tipos_declarados if t not in definidos]


def leer_nupkg(ruta_relativa, datos):
    """Lee el .nupkg de la raíz del repositorio y lo devuelve en base64, que es
    como viaja en la columna `content`. Lanza `Bloqueado` si no está armado:
    no es un error del playbook, es que falta correr `dotnet pack`. También
    lanza `Bloqueado` si los .dll de adentro no traen alguno de los `tipos`
    declarados: subirlo así sube un paquete que no sirve para nada. Y `ErrorNupkg`
    si el nuspec no es el del paquete declarado o le faltan dependencias."""
    tipos_declarados = datos["tipos"]
    ruta = os.path.join(_RAIZ, ruta_relativa)
    if not os.path.isfile(ruta):
        raise Bloqueado(
            f"no existe el paquete '{ruta_relativa}'; hay que armarlo antes con "
            "`dotnet pack <proyecto de empaquetado>.csproj -c Release`"
        )
    with open(ruta, "rb") as f:
        crudo = f.read()
    if not crudo:
        raise Bloqueado(f"el paquete '{ruta_relativa}' está vacío")
    if len(crudo) > MAXIMO_BYTES_NUPKG:
        raise Bloqueado(
            f"el paquete '{ruta_relativa}' pesa {len(crudo)} bytes, más que el tope de {MAXIMO_BYTES_NUPKG}; "
            "casi siempre significa que se coló un ensamblado que ya vive en el sandbox (el SDK de Dataverse)"
        )
    with _abrir_nupkg(crudo) as paquete:
        _exigir_el_paquete_declarado(paquete, datos)
    faltan = _tipos_faltantes_en_dll(crudo, tipos_declarados)
    if faltan:
        raise Bloqueado(
            f"el paquete '{ruta_relativa}' no trae estos tipos declarados en el playbook: {faltan}. "
            "Es la señal de que `dotnet pack` no regeneró el .nupkg (MSBuild puede dar `GenerateNuspec` "
            "por \"up-to-date\" contra una caché de `obj/` vieja): borrar `obj/` y `bin/` a mano y volver "
            "a empaquetar."
        )
    return crudo, base64.b64encode(crudo).decode("ascii")


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
def _leer_paquete(dv, uniquename):
    """El paquete por su unique name, o `None`. Nunca devuelve más de uno: el
    unique name es único, y si el entorno devolviera dos es forma inesperada."""
    ruta = (
        "pluginpackages?$select=pluginpackageid,name,uniquename,version,ismanaged"
        f"&$filter=uniquename eq '{uniquename}'"
    )
    cuerpo = leer_entorno(dv, ruta, C_PAQUETE)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_PAQUETE, "value")
    if len(filas) > 1:
        raise ErrorEntorno(f"{C_PAQUETE} devolvió {len(filas)} filas para el unique name '{uniquename}'; se esperaba a lo sumo una")
    return filas[0] if filas else None


def _leer_ensamblados(dv, paquete_id):
    """Los `pluginassembly` que la plataforma registró sola a partir del paquete, con su `version` (el
    AssemblyVersion) validada por tipo. `modifiedon` viaja en la fila pero solo se valida donde se usa."""
    ruta = (
        "pluginassemblies?$select=pluginassemblyid,name,version,modifiedon"
        f"&$filter=_packageid_value eq {paquete_id}"
    )
    cuerpo = leer_entorno(dv, ruta, C_ENSAMBLADOS)
    ensamblados = exigir_forma(cuerpo.get("value"), [dict], C_ENSAMBLADOS, "value")
    for asm in ensamblados:
        exigir_forma(asm.get("pluginassemblyid"), str, C_ENSAMBLADOS, "pluginassemblyid", no_vacio=True)
        exigir_forma(asm.get("name"), str, C_ENSAMBLADOS, "name")
        exigir_forma(asm.get("version"), str, C_ENSAMBLADOS, "version")
    return ensamblados


def _fecha_de_modificacion(asm):
    """`modifiedon` de una fila de ensamblado como fecha. Un valor ausente, que no sea texto o que no sea una
    fecha ISO 8601 es forma inesperada: no se puede decidir si el binario nuevo se cargó."""
    texto = exigir_forma(asm.get("modifiedon"), str, C_ENSAMBLADOS, "modifiedon", no_vacio=True)
    try:
        fecha = datetime.datetime.fromisoformat(texto)
    except ValueError:
        raise ErrorEntorno(
            f"{C_ENSAMBLADOS} devolvió 200 con forma inesperada: 'modifiedon' debía ser una fecha ISO 8601 y llegó {texto!r}"
        ) from None
    return fecha if fecha.tzinfo else fecha.replace(tzinfo=datetime.timezone.utc)


def _modificaciones_por_ensamblado(ensamblados):
    return {asm["name"]: (_fecha_de_modificacion(asm), asm["modifiedon"]) for asm in ensamblados}


def _tipos_registrados(dv, ensamblados):
    """Los `plugintype` que la plataforma registró a partir de esos ensamblados: {typename: plugintypeid}."""
    tipos = {}
    for asm in ensamblados:
        ruta_tipos = f"plugintypes?$select=plugintypeid,typename&$filter=_pluginassemblyid_value eq {asm['pluginassemblyid']}"
        cuerpo_tipos = leer_entorno(dv, ruta_tipos, C_TIPOS)
        for t in exigir_forma(cuerpo_tipos.get("value"), [dict], C_TIPOS, "value"):
            nombre_tipo = exigir_forma(t.get("typename"), str, C_TIPOS, "typename", no_vacio=True)
            tipos[nombre_tipo] = exigir_forma(t.get("plugintypeid"), str, C_TIPOS, "plugintypeid", no_vacio=True)
    return tipos


def _diferencias_de_modificacion(antes, despues):
    """Tras `--actualizar-contenido`: cada ensamblado que ya existía tiene que haber avanzado su `modifiedon`. Un 204
    del PATCH solo dice que la plataforma recibió el contenido; si `modifiedon` no se movió, el sandbox sigue con el
    binario anterior (típicamente porque no subió el AssemblyVersion)."""
    difs = []
    for nombre, (fecha_antes, texto_antes) in sorted(antes.items()):
        if nombre not in despues:
            difs.append(f"el ensamblado '{nombre}' ya no figura en el paquete después de actualizar el contenido")
            continue
        fecha_despues, texto_despues = despues[nombre]
        if fecha_despues <= fecha_antes:
            difs.append(
                f"modifiedon de '{nombre}' no avanzó (antes {texto_antes}, después {texto_despues}): la plataforma pudo "
                "no cargar el binario nuevo y seguir ejecutando el anterior. Subir el AssemblyVersion (tercer número), "
                "volver a empaquetar y repetir"
            )
    return difs


def _componente_de_la_solucion(dv, solution_id, paquete_id):
    """Fila de `solutioncomponents` del paquete en esa solución, si está.

    A propósito NO se filtra por `componenttype`: Learn avisa que los tipos de
    componente nuevos (paquete de plugins, Custom API) no están en el optionset
    público `componenttype`, y el número que circula en foros no está
    verificado. Se lee el que la plataforma haya puesto y se informa: así el
    ensayo con componentes descartables deja la evidencia en vez de la
    suposición."""
    ruta = (
        "solutioncomponents?$select=componenttype,objectid"
        f"&$filter=_solutionid_value eq {solution_id} and objectid eq {paquete_id}"
    )
    cuerpo = leer_entorno(dv, ruta, C_COMPONENTES)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_COMPONENTES, "value")
    return filas[0] if filas else None


def _verificar(dv, datos, solucion, solution_id, paquete):
    """Todo lo que tiene que ser cierto después de subir el paquete. Devuelve
    la lista de diferencias (vacía = está bien) y el detalle para el informe."""
    difs = []
    paquete_id = exigir_forma(paquete.get("pluginpackageid"), str, C_PAQUETE, "pluginpackageid", no_vacio=True)

    nombre = exigir_forma(paquete.get("name"), str, C_PAQUETE, "name")
    version = exigir_forma(paquete.get("version"), str, C_PAQUETE, "version")
    if nombre != datos["nombre"]:
        difs.append(f"name es {nombre!r} y el playbook dice {datos['nombre']!r} (INMUTABLE: hay que borrar el paquete y rehacerlo)")
    if version != datos["version"]:
        difs.append(f"version es {version!r} y el playbook dice {datos['version']!r} (INMUTABLE: hay que borrar el paquete y rehacerlo)")
    if exigir_forma(paquete.get("ismanaged"), bool, C_PAQUETE, "ismanaged"):
        difs.append("el paquete está managed en este entorno; no se toca desde acá")

    filas_ensamblados = _leer_ensamblados(dv, paquete_id)
    ensamblados = sorted(asm["name"] for asm in filas_ensamblados)
    tipos = _tipos_registrados(dv, filas_ensamblados)
    # `pluginassemblies.version` es el AssemblyVersion que cargó el sandbox. Si el binario cambió y la versión no,
    # la plataforma sigue ejecutando el anterior sin avisar (alm.md): por eso se compara en los tres caminos.
    for asm in sorted(filas_ensamblados, key=lambda a: a["name"]):
        if asm["version"] != datos["version_ensamblado"]:
            difs.append(
                f"el ensamblado '{asm['name']}': version es {asm['version']!r} y el playbook dice "
                f"version_ensamblado {datos['version_ensamblado']!r} (si el binario cambió, subir el AssemblyVersion del "
                "proyecto de empaquetado y volver a empaquetar; si no, corregir version_ensamblado)"
            )
    faltan = [t for t in datos["tipos"] if t not in tipos]
    if faltan:
        difs.append(
            f"la plataforma no registró estos tipos de plugin: {faltan}. "
            f"Ensamblados del paquete: {ensamblados or 'ninguno'}. Tipos registrados: {sorted(tipos) or 'ninguno'}"
        )
    # Una verificación que solo mira lo que FALTA miente si algo sobra: un tipo
    # de una derivación previa puede quedar registrado en el entorno sin que
    # ningún playbook lo declare.
    sobran = sorted(set(tipos) - set(datos["tipos"]))
    if sobran:
        difs.append(
            f"la plataforma tiene tipos de plugin registrados que el playbook no declara (sobran): {sobran}. "
            "Puede ser una derivación previa: agregarlos a `tipos` si siguen sirviendo, o borrar el step/Custom "
            "API que apunte a ellos y después el tipo, si ya no sirven"
        )

    componente = _componente_de_la_solucion(dv, solution_id, paquete_id)
    if componente is None:
        difs.append(fuera_de_la_solucion("el paquete", solucion, solution_id))
        tipo_componente = None
    else:
        tipo_componente = componente.get("componenttype")

    return difs, {
        "paquete_id": paquete_id, "tipos": tipos, "ensamblados": ensamblados, "filas_ensamblados": filas_ensamblados,
        "componenttype": tipo_componente,
    }


def _contra_entorno(dv, datos, identidad, solo_verificar, actualizar_contenido, componente, dormir):
    solucion = identidad["solucion"]
    solution_id = comprobar_solucion_e_idioma(dv, identidad)
    paquete = _leer_paquete(dv, datos["uniquename"])
    antes = None  # {ensamblado: modifiedon} previo al PATCH; solo existe con --actualizar-contenido

    if paquete is None:
        if solo_verificar:
            return "difiere", componente, f"el paquete '{datos['uniquename']}' no existe en el entorno"
        crudo, contenido = leer_nupkg(datos["nupkg"], datos)
        cuerpo = {
            "name": datos["nombre"],
            "uniquename": datos["uniquename"],
            "version": datos["version"],
            "content": contenido,
        }
        print(f"Subo el paquete: {len(crudo)} bytes ({len(contenido)} en base64) a la solución '{solucion}'.", flush=True)
        est, resp, _ = escribir_metadatos(dv, "POST", "pluginpackages", cuerpo, dormir=dormir, solucion=solucion)
        if est not in (200, 201, 204):
            return "error", componente, f"el alta del paquete devolvió HTTP {est} (se esperaba 204): {resp}"
        paquete = _leer_paquete(dv, datos["uniquename"])
        if paquete is None:
            return "error", componente, "el alta del paquete no devolvió error pero el paquete no aparece al releer"
        accion = "creado"
    else:
        accion = "ya_existia"
        if actualizar_contenido and not solo_verificar:
            crudo, contenido = leer_nupkg(datos["nupkg"], datos)
            paquete_id = exigir_forma(paquete.get("pluginpackageid"), str, C_PAQUETE, "pluginpackageid", no_vacio=True)
            # El estado de los ensamblados ANTES del PATCH (también validado antes de escribir): sin él no hay con qué
            # comparar para saber si el binario nuevo se cargó.
            antes = _modificaciones_por_ensamblado(_leer_ensamblados(dv, paquete_id))
            print(f"Actualizo el contenido del paquete: {len(crudo)} bytes ({len(contenido)} en base64).", flush=True)
            est, resp, _ = escribir_metadatos(
                dv, "PATCH", f"pluginpackages({paquete_id})", {"content": contenido}, dormir=dormir, solucion=solucion
            )
            if est not in (200, 204):
                return "error", componente, f"la actualización del contenido devolvió HTTP {est} (se esperaba 204): {resp}"
            paquete = _leer_paquete(dv, datos["uniquename"])
            accion = "actualizado"  # se reemplazó el contenido de un paquete que ya existía (aprobado 2026-10-01)

    difs, detalle = _verificar(dv, datos, solucion, solution_id, paquete)
    if antes is not None:
        difs.extend(_diferencias_de_modificacion(antes, _modificaciones_por_ensamblado(detalle["filas_ensamblados"])))
    if difs:
        estado = "difiere" if accion == "ya_existia" else "error"
        prefijo = "" if accion == "ya_existia" else "se escribió pero no quedó bien: "
        return estado, componente, prefijo + "; ".join(difs)

    fechas = ""
    if antes is not None:
        despues = _modificaciones_por_ensamblado(detalle["filas_ensamblados"])
        fechas = "; " + ", ".join(
            f"'{nombre}' modifiedon {texto_antes} -> {despues[nombre][1]}" for nombre, (_, texto_antes) in sorted(antes.items())
        )
    return accion, componente, (
        f"pluginpackageid {detalle['paquete_id']}, version {datos['version']}, "
        f"ensamblados {detalle['ensamblados']} (version {datos['version_ensamblado']}), "
        f"{len(detalle['tipos'])} tipos de plugin registrados, "
        f"en la solución '{solucion}' con componenttype {detalle['componenttype']}{fechas}"
    )


def construir(ruta_playbook, solo_verificar, fabrica_cliente, actualizar_contenido=False, dormir=None):
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
        if isinstance(datos.get("uniquename"), str):
            componente = datos["uniquename"]
        paso = "leer el bloque '## 1. Identidad'"
        identidad = obtener_identidad(secciones)
        paso = "validar el bloque del paquete"
        validar_playbook(datos, identidad)
    except ErrorPlaybook as e:
        return "error", componente, str(e)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except OSError as e:
        return "error", componente, f"no se pudo leer el playbook '{ruta_playbook}': {e}"
    except Exception as e:
        return "error", componente, f"fallo inesperado validando el playbook (offline), al {paso}: {type(e).__name__}: {e}"

    print(f"Playbook leído y validado (offline). tipo={TIPO} uniquename={componente}")
    try:
        dv = fabrica_cliente()
    except Exception:
        return "error", componente, MENSAJE_FALLO_CLIENTE

    rastro = Rastro(dv)
    try:
        return _contra_entorno(rastro, datos, identidad, solo_verificar, actualizar_contenido, componente, dormir)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except ErrorNupkg as e:
        return "error", componente, str(e)
    except ErrorEntorno as e:
        return "error", componente, str(e)
    except Exception as e:
        return "error", componente, f"fallo inesperado hablando con Dataverse, durante {rastro.en_curso}: {type(e).__name__}: {e}"


def main():
    argv = sys.argv[1:]
    rutas = [a for a in argv if not a.startswith("--")]
    banderas = {a for a in argv if a.startswith("--")}
    desconocidas = sorted(banderas - {"--solo-verificar", "--actualizar-contenido"})
    if len(rutas) != 1 or desconocidas:
        return salida(
            "error", "desconocido",
            "uso incorrecto: paquete_plugins.py <playbook.md> [--solo-verificar] [--actualizar-contenido]"
            + (f"; banderas desconocidas: {desconocidas}" if desconocidas else ""),
        )
    from dataverse_api import Dataverse

    estado, componente, detalle = construir(
        rutas[0], "--solo-verificar" in banderas, Dataverse, actualizar_contenido="--actualizar-contenido" in banderas
    )
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
