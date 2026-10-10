# Playbook: rol · SR - CARP - Jefe

## 1. Identidad

```json
{
  "tipo_playbook": "rol",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "6.3",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. El rol está definido en `diseno/04-matriz-privilegios.md` (columna Jef, sección 2) y figura en `diseno/06-inventario-componentes.md` §6, renglón 6.3 (en la definición: "ARP Jefe").

Advertencias: un rol no lleva etiquetas por idioma (es un texto único), así que `idiomas` solo cumple el formato común. El nombre va sin tildes (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "rol",
  "nombre": "SR - CARP - Jefe",
  "descripcion": "Jefe del área de arreglos de pago: suma al oficial el mantenimiento de las versiones de plantilla y de su mapeo de celdas. Se asigna además del rol Oficial, nunca solo.",
  "base": "App Opener",
  "tablas": {
    "sanic_carp_tbl_versionplantilla": {
      "crear": "unidad",
      "escribir": "unidad",
      "anexar": "unidad"
    },
    "sanic_carp_tbl_mapeocelda": {
      "crear": "unidad",
      "escribir": "unidad",
      "anexar": "unidad",
      "borrar": "unidad"
    }
  },
  "otros_privilegios": {}
}
```

Un security role en la unidad de negocio raíz, con los privilegios del rol `App Opener` (los que tenga el entorno en el momento de construir) más los 10 de este playbook. **Solo lo adicional al Oficial** (decisión del 2026-10-09, definición línea 393): no repite ningún privilegio del rol `SR - CARP - Oficial`. Alcances: `usuario` = Basic (solo lo propio) · `unidad` = Local (la BU `Arreglos de Pago`) · `unidad_e_hijas` = Deep · `organizacion` = Global.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del rol | «Jefe del área de arreglos de pago: suma al oficial el mantenimiento de las versiones de plantilla y de su mapeo de celdas. Se asigna además del rol Oficial, nunca solo.» | La matriz trae el nombre y los privilegios; el texto lo fija el playbook |
| Rol base | `App Opener` | `rol.py` exige un rol base y la convención del estudio es `App Opener` (`rol.md` §1). Aporta lo que la app necesita para abrirse y nada sobre tablas de ARP; repite lo que ya trae el Oficial, pero no suma privilegios nuevos. Duda D-05 de la matriz |

Viene del diseño, no lo decide este playbook:

- Privilegios: `04` §2, columna Jef (definición línea 394): "Create, Write y Append sobre Versión de plantilla y Mapeo, Delete solo sobre Mapeo en Borrador".
- El Delete de Mapeo es el **único Delete de un rol humano**; "solo en Borrador" lo impone el plugin de Versión y Mapeo (inventario 7.16), no este rol.
- `leer` y `anexar_a` de Versión de plantilla **no están acá**: los trae el Oficial (definición línea 390), que el Jefe también tiene.
- La reasignación y la reapertura las comprueba el plugin por pertenencia al team `Arreglos de Pago - Jefatura`, no este rol (línea 394).
- Las vistas y el dashboard de supervisión no son privilegios: van con la app (inventario 12).
- La vista previa de plantilla y la publicación usan `escribir` sobre Versión de plantilla (definición línea 342).
- `anexar` de Mapeo es el lado hijo del lookup a Versión de plantilla (REL-08); el lado padre (`anexar_a` de Versión) lo trae el Oficial.
- No tiene `asignar`: la definición solo lo da al Oficial, en Cliente y Arreglo (D-31).
- **Huecos de la matriz resueltos por el arquitecto el 2026-10-09** (D-01 y D-02 de `diseno/04-matriz-privilegios.md` §5): se agregan `leer` de Mapeo (sin él el Jefe no podría abrir lo que mantiene) y `asignar` de Versión y de Mapeo (el plugin de P-11 pone al team como dueño en el alta, y crear un registro con otro dueño exige Assign, igual que D-31 para el Oficial). Ambos se confirman en la prueba con un usuario no administrador.

## 3. Precondiciones

Las de la receta (`seguridad/patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | los de la sección 1 |
| 2 | Hay exactamente una unidad de negocio raíz | una |
| 3 | El rol base existe una vez en la unidad raíz | `App Opener` |
| 4 | Cada tabla nombrada existe, y cada privilegio admite el alcance pedido | `sanic_carp_tbl_versionplantilla` · `sanic_carp_tbl_mapeocelda` |
| 5 | El nombre no lleva tildes ni ñ (BP-PP-197) y cumple `SR - CARP - Nombre` | el de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/seguridad/patrones.md` §2.1 "Security role". Herramienta del proyecto: `herramientas/construir/rol.py`.

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_jefe.md
```

## 5. Verificación

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_jefe.md --solo-verificar
python3 herramientas/construir/muestra_rol.py playbooks/rol/sr_carp_jefe.md --guardar
```

Privilegios sobre las tablas de la solución. **Exactamente estos**: uno de menos, uno de más u otro alcance es `difiere`.

| Tabla | Privilegios esperados |
|---|---|
| `sanic_carp_tbl_versionplantilla` | `prvCreatesanic_carp_tbl_versionplantilla` = Local · `prvWritesanic_carp_tbl_versionplantilla` = Local · `prvAppendsanic_carp_tbl_versionplantilla` = Local |
| `sanic_carp_tbl_mapeocelda` | `prvCreatesanic_carp_tbl_mapeocelda` = Local · `prvWritesanic_carp_tbl_mapeocelda` = Local · `prvAppendsanic_carp_tbl_mapeocelda` = Local · `prvDeletesanic_carp_tbl_mapeocelda` = Local |

Otros privilegios: ninguno.

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET roles` por nombre en la unidad raíz | un rol · `ismanaged = false` · la descripción de la sección 2 |
| 2 | `RetrieveRolePrivilegesRole` | los privilegios de `App Opener` más los 10 de arriba; **ninguno más** (en particular, ningún privilegio de Email, Queue, Cliente, Conversación, Arreglo, Cuota ni Generación de formato: son del Oficial) |
| 3 | Pertenencia a la solución | el rol figura una vez en la solución de la sección 1 (`solutioncomponents`, tipo 20) |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | Comprobación independiente contra el XML exportado (`muestra_rol.py`) | `OK`, y la muestra queda en `playbooks/rol/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. La herramienta nunca quita un privilegio ni cambia un alcance. Si lo único que pasa es que **faltan** privilegios (una corrida cortada, o una actualización de Microsoft que amplió `App Opener`), se completa con `--completar`; eso lo decide quien dirige la construcción, no el constructor.

## 7. Reversa

`DELETE roles(<roleid>)`. No es posible mientras el rol esté asignado a un usuario o a un team. Nada es irreversible: el nombre, la descripción y los privilegios se pueden cambiar después; pero **cambiar un rol ya asignado cambia lo que esas personas pueden hacer en el momento**. Un usuario no ve los privilegios agregados a un rol ya asignado hasta que se le quita y se le vuelve a asociar el rol (spike 4, hallazgo 1).

## 8. Fuera de alcance

- Asignar el rol al team `Arreglos de Pago - Jefatura` (el jefe suma este rol y el Oficial): tarea de administración 13.7, inventario 13. La copia del rol en la BU hija la crea la plataforma sola.
- Dar al rol acceso a la app model-driven y a las vistas y el dashboard de supervisión: van con la app, inventario 12.
- El perfil de seguridad de columna y su asociación al team Jefatura: inventarios 6.1 y 13.8.
- Los privilegios de ejecución de las Custom API: inventario 8.2 a 8.6 (diseño detallado pendiente). La definición los apoya en privilegios de tabla que el Jefe ya tiene por el Oficial; este rol no agrega ninguno para ellas.
- La prueba con un usuario no administrador de que `asignar` hace falta para crear con el team como dueño (D-02): si no hace falta, se quita.
