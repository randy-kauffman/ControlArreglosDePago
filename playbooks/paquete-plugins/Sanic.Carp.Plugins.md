# Playbook: paquete-plugins · sanic_carp_pkg_plugins

> **Piloto de un tipo con 0 ciclos** (`power-platform-especificar`, `references/paquete-plugins.md`). Tiene huecos de diseño marcados abajo: **no se construye hasta que el dueño los cierre**.

## 1. Identidad

```json
{
  "tipo_playbook": "paquete-plugins",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "8.1",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_controlarreglospago",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "carp",
  "prefijo_opciones": 15946,
  "idiomas": [
    1033,
    3082
  ]
}
```

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. El paquete figura en `diseno/06-inventario-componentes.md` §8, renglón 8.1; su nombre, en `diseno/01-convenciones.md` §2 (fila «Paquete de plugins») y §5; su estructura, en `diseno/03-contratos-custom-api.md` §4.1.

Advertencias: un paquete no lleva etiquetas por idioma, así que `idiomas` solo cumple el formato común. En Dev hay otro publisher con el mismo prefijo `sanic` («Sanic Corp»): la herramienta identifica el publisher por unique name y se detiene si no coincide. `version_skill` es la de la skill al escribir este piloto; la referencia del tipo se agregó el 2026-10-20 sin subir la versión de la skill.

## 2. Qué se crea

```json
{
  "tipo": "paquete-plugins",
  "nombre": "sanic_carp_pkg_plugins",
  "uniquename": "sanic_carp_pkg_plugins",
  "version": "1.0.0",
  "version_ensamblado": "1.0.0.0",
  "nupkg": "src/Sanic.Carp.Plugins.Paquete/bin/Release/sanic_carp_pkg_plugins.1.0.0.nupkg",
  "tipos": [
    "Sanic.Carp.Plugins.Api.GenerarFormatoApi",
    "Sanic.Carp.Plugins.Api.IniciarConversacionNuevaApi",
    "Sanic.Carp.Plugins.Api.ReconciliarCorreosApi",
    "Sanic.Carp.Plugins.Api.TomarConversacionApi",
    "Sanic.Carp.Plugins.Api.UnirConversacionesApi",
    "Sanic.Carp.Plugins.Steps.ArchivoDePlantillaStep",
    "Sanic.Carp.Plugins.Steps.ArregloActualizarStep",
    "Sanic.Carp.Plugins.Steps.ArregloCalendarioStep",
    "Sanic.Carp.Plugins.Steps.ArregloCrearStep",
    "Sanic.Carp.Plugins.Steps.ClienteStep",
    "Sanic.Carp.Plugins.Steps.ConversacionActualizarStep",
    "Sanic.Carp.Plugins.Steps.ConversacionAsignacionStep",
    "Sanic.Carp.Plugins.Steps.ConversacionCrearStep",
    "Sanic.Carp.Plugins.Steps.CuotaActualizarStep",
    "Sanic.Carp.Plugins.Steps.CuotaTerminarArregloStep",
    "Sanic.Carp.Plugins.Steps.EmailSoloSistemaStep",
    "Sanic.Carp.Plugins.Steps.MapeoCeldaActualizarStep",
    "Sanic.Carp.Plugins.Steps.MapeoCeldaCrearStep",
    "Sanic.Carp.Plugins.Steps.MapeoCeldaEliminarStep",
    "Sanic.Carp.Plugins.Steps.PropietarioAlActualizarStep",
    "Sanic.Carp.Plugins.Steps.PropietarioAlCrearStep",
    "Sanic.Carp.Plugins.Steps.RegistrarCorreoEntranteStep",
    "Sanic.Carp.Plugins.Steps.RemitenteDelAreaStep",
    "Sanic.Carp.Plugins.Steps.RespuestaEnviadaStep",
    "Sanic.Carp.Plugins.Steps.VersionPlantillaActualizarStep",
    "Sanic.Carp.Plugins.Steps.VersionPlantillaCrearStep"
  ]
}
```

Un paquete de plugins con el ensamblado `Sanic.Carp.Plugins` (.NET Framework 4.6.2, sin firmar) y su dependencia `DocumentFormat.OpenXml` 2.20.0 como ensamblado dependiente (no firmar el ensamblado propio es a propósito: obligaría a firmar sus dependencias, `diseno/01-convenciones.md` §5). Los 26 tipos son las clases `IPlugin` concretas del código: las **5 Custom API** (`diseno/03` §1, «Registro» de cada una; clases de `src/Sanic.Carp.Plugins/Api/`) y las **21 clases de step** de `diseno/03` §5 (clases de `src/Sanic.Carp.Plugins/Steps/`). `BaseDeApi` y `BaseDeStep` son abstractas y no se registran. Namespace raíz `Sanic.Carp.Plugins`: **inmutable tras el primer registro**.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| `nombre` igual a `uniquename` | `sanic_carp_pkg_plugins` | La convención dice «no aplica» para el nombre visible y solo da el unique name. Los paquetes de los spikes 2026-003 se registraron con `name` = `uniquename`, y un `name` en texto libre fue rechazado con 400 |
| `version` del paquete | `1.0.0` | **Hueco de diseño.** `diseno/03` §4.1 no la fija; el `.csproj` solo declara `AssemblyVersion` 1.0.0.0, que no es la versión de paquete. `1.0.0` es la que usaron todos los spikes del proyecto. **Irreversible**: el dueño la confirma antes de construir |
| `version_ensamblado` | `1.0.0.0` | El `AssemblyVersion` del ensamblado `Sanic.Carp.Plugins` (`<AssemblyVersion>` de los dos `.csproj`), que la plataforma guarda en `pluginassemblies.version`. **No es la `version` del paquete.** La herramienta compara contra él lo que lee del entorno y, con `--actualizar-contenido`, exige que `modifiedon` haya avanzado: **hay que subir el tercer número en cada despliegue** y actualizar este valor a la par (`alm.md`) |
| Ruta del `.nupkg` | `src/Sanic.Carp.Plugins.Paquete/bin/Release/sanic_carp_pkg_plugins.1.0.0.nupkg` | **Ruta cerrada (2026-10-20).** El `.csproj` de producción no declara `PackageId` ni empaquetado de plugins; lo hace el proyecto aparte `src/Sanic.Carp.Plugins.Paquete/` (`PackageId` `sanic_carp_pkg_plugins`, `Version` `1.0.0`) y la ruta quedó confirmada con una corrida real. La `version` del paquete sigue siendo del dueño (irreversible) |
| Lista `tipos` | Las 26 clases | Salen del código (clases `sealed` que heredan de `BaseDeApi` o `BaseDeStep`), no de memoria. `diseno/03` §5 cuenta 21 clases de step y 5 API |

Declarado en prosa porque la herramienta no lo lee (no son claves del bloque):

- `AssemblyVersion` y `FileVersion` esperados del ensamblado propio: **1.0.0.0** (`src/Sanic.Carp.Plugins/Sanic.Carp.Plugins.csproj`). En cada despliegue posterior se sube **solo el tercer número**.
- Dependencias que tienen que viajar dentro del `.nupkg`: `DocumentFormat.OpenXml` 2.20.0. **No** viaja `Microsoft.CrmSdk.CoreAssemblies` (el csproj lo marca `PrivateAssets=All`: ya está en el sandbox).
- Destino de compilación que ejecuta el sandbox: `net462`.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Consulta | Esperado |
|---|---|---|---|
| 1 | La solución existe, no es managed, su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | `solutions?$filter=uniquename eq '<solucion>'` con el publisher expandido; `organizations`; `RetrieveProvisionedLanguages` | los de la sección 1 |
| 2 | El `.nupkg` existe en la ruta de la sección 2, no está vacío, pesa menos de 12 MB y trae cada tipo de `tipos` dentro de su `.dll` bajo la carpeta de ensamblados | lectura del archivo | los 26 tipos presentes |
| 3 | El código está compilado y probado | inventario 7.1 a 7.22 | `hecho` (2026-10-20, 5820 pruebas en verde) |
| 4 | Nombre y `uniquename` sin tildes ni ñ, uniquename con el prefijo `sanic_`, `nombre` igual a `uniquename` y `version_ensamblado` de cuatro números | sin red | el de la sección 2 |
| 5 | **El hueco de la versión y de la ruta del `.nupkg` está cerrado por el dueño** | decisión escrita en `diseno/03` §4.1 | confirmación; mientras no esté, el constructor no corre |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/dataverse/alm.md` («El `AssemblyVersion` es parte del despliegue») y `references/herramientas/patrones.md`. Herramienta del proyecto: `herramientas/construir/paquete_plugins.py`.

```
python3 herramientas/construir/paquete_plugins.py playbooks/paquete-plugins/Sanic.Carp.Plugins.md
```

El empaquetado del `.nupkg` **no es de este playbook ni del constructor**: la orden del constructor de código prohíbe `dotnet pack`. Lo hace el proyecto de empaquetado `src/Sanic.Carp.Plugins.Paquete/` (`diseno/03` §4.1), con `dotnet build` (no `pack`: falla con NU5128; el targets de `Microsoft.PowerApps.MSBuild.Plugin` arma el `.nupkg`). Regla de la receta: borrar a mano `bin/` y `obj/` de los dos proyectos antes de empaquetar; `dotnet clean` no alcanza. Comando, desde la raíz del repo:

```
rm -rf src/Sanic.Carp.Plugins.Paquete/obj src/Sanic.Carp.Plugins.Paquete/bin src/Sanic.Carp.Plugins/obj src/Sanic.Carp.Plugins/bin
dotnet build src/Sanic.Carp.Plugins.Paquete -c Release
```

Verificado el 2026-10-20: genera `src/Sanic.Carp.Plugins.Paquete/bin/Release/sanic_carp_pkg_plugins.1.0.0.nupkg` con `lib/net462/Sanic.Carp.Plugins.dll` y `lib/net462/DocumentFormat.OpenXml.dll`, los 26 tipos dentro del `.dll`, sin `lib/net8.0`.

## 5. Verificación

```
python3 herramientas/construir/paquete_plugins.py playbooks/paquete-plugins/Sanic.Carp.Plugins.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET pluginpackages?$filter=uniquename eq 'sanic_carp_pkg_plugins'` | un paquete · `name` `sanic_carp_pkg_plugins` · `version` `1.0.0` · `ismanaged = false` |
| 2 | `GET pluginassemblies` y `GET plugintypes` del paquete | **exactamente** los 26 tipos de la sección 2: uno que falta y uno de más son `error`/`difiere` |
| 3 | `GET solutioncomponents` de la solución por el `pluginpackageid` | una fila; **se informa el `componenttype`** que puso la plataforma, no se compara con un número |
| 4 | Contenido del `.nupkg` antes de subir (en el alta y con `--actualizar-contenido`; si falla, no se escribe nada) | el `.nuspec` dice `id` = `uniquename` y `version` = `version`; cada dependencia del nuspec que no es de plataforma (`DocumentFormat.OpenXml` entre ellas) tiene su `.dll` en `lib/net462/`; cada uno de los 26 tipos está definido, con su namespace completo, en un `.dll` de `lib/net462/` |
| 5 | `GET pluginassemblies?$select=name,version,modifiedon&$filter=_packageid_value eq <id>` (la herramienta lo hace en los tres caminos) | cada ensamblado con `version` = `version_ensamblado` (`1.0.0.0`). Con `--actualizar-contenido` se lee antes y después del `PATCH` y `modifiedon` tiene que haber avanzado: si no, `error` |
| 6 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Si `name` o `version` difieren, la herramienta lo dice y para: son inmutables y se resuelven borrando a mano. **El contenido solo se reemplaza con `--actualizar-contenido`**, y eso lo decide quien dirige la construcción, no el constructor, y siempre con el tercer número de `AssemblyVersion` subido.

## 7. Reversa

`DELETE pluginpackages(<pluginpackageid>)`. **Se lleva puestos los ensamblados, los tipos y todos los steps registrados sobre ellos**, y las Custom API que apuntaban a un tipo quedan sin plugin. Irreversibles una vez creado: `name`, `uniquename` y `version`, y el namespace raíz de los tipos (renombrar un tipo rompe el registro en silencio). Lo que sí se puede cambiar es el contenido.

## 8. Fuera de alcance

- Compilar y empaquetar el código (`dotnet pack`): inventario 7.1 a 7.22 y el proyecto de empaquetado, pendiente de definir.
- Registrar las cinco Custom API: inventario 8.2 a 8.6.
- Registrar los 37 steps: inventario 8.7 (`playbooks/steps/sanic_carp_pkg_plugins.md`).
- Los privilegios de ejecución de las API en los roles: inventario 6.2 a 6.5.
- Subir de versión el paquete en un despliegue posterior: otra corrida con `--actualizar-contenido`, decidida por quien dirige.
