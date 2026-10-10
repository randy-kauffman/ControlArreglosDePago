# Playbook: perfil de seguridad de columna · CSP - CARP - Datos sensibles

## 1. Identidad

```json
{
  "tipo_playbook": "perfil-columnas",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "6.1",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El perfil está definido en `diseno/02-diccionario-datos.md` §8 y figura en `diseno/06-inventario-componentes.md` §6.1, renglón 6.1. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas, 4 relaciones, 5 claves, 6 seguridad.

Advertencias: el perfil no tiene nombre lógico (BP-PP-185) y su nombre es un texto único, sin etiquetas por idioma, por eso `idiomas` solo se usa para comprobar el entorno. El nombre no lleva tildes ni ñ (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "perfil-columnas",
  "nombre": "CSP - CARP - Datos sensibles",
  "descripcion": "Lectura y escritura de la cédula del cliente y de la cuenta a debitar, y lectura de la cédula copiada en el arreglo. Se da al team ARP y al team ARP Jefatura.",
  "permisos": [
    {
      "tabla": "sanic_carp_tbl_cliente",
      "columna": "sanic_cedula",
      "leer": true,
      "crear": true,
      "actualizar": true
    },
    {
      "tabla": "sanic_carp_tbl_arreglo",
      "columna": "sanic_cuentadebitar",
      "leer": true,
      "crear": true,
      "actualizar": true
    },
    {
      "tabla": "sanic_carp_tbl_arreglo",
      "columna": "sanic_instantaneacedula",
      "leer": true,
      "crear": false,
      "actualizar": false
    }
  ]
}
```

Un único perfil de seguridad de columna con **tres permisos**, uno por columna protegida del modelo. **Sin miembros**: a quién se da se resuelve fuera de la solución (sección 8). "Leer sin enmascarar" queda siempre en no (la herramienta lo escribe en 0).

| Tabla | Columna | Leer | Crear | Actualizar | Motivo (diccionario §8, P-16) |
|---|---|---|---|---|---|
| `sanic_carp_tbl_cliente` | `sanic_cedula` | sí | sí | sí | El oficial busca por cédula (RF-13) y la digita al crear el cliente; sin perfil, la fila no se encuentra por cédula y la columna llega vacía (D-19, spike 4) |
| `sanic_carp_tbl_arreglo` | `sanic_cuentadebitar` | sí | sí | sí | El oficial la digita en el borrador y la corrige antes del congelamiento (RF-06) |
| `sanic_carp_tbl_arreglo` | `sanic_instantaneacedula` | sí | no | no | La escribe el sistema al congelar (D-30); el oficial solo la lee |

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Texto de la descripción | El del diccionario §8, tal cual | El diccionario trae la descripción; el playbook no la reescribe. Nota: nombra «team ARP» y «team ARP Jefatura», mientras que el inventario 13.2 y 13.3 los llama `Arreglos de Pago` y `Arreglos de Pago - Jefatura` (ver dudas en la entrega) |
| Leer sin enmascarar | No permitido | Las columnas no tienen regla de enmascarado; es el valor por defecto y el más restrictivo, y el formato del playbook no lo admite |
| Un solo perfil, un solo nivel de acceso | Los dos teams reciben los mismos permisos | La matriz del diccionario §8 no pide accesos distintos para grupos distintos sobre las mismas columnas; si se pidieran, haría falta un perfil por nivel (`perfil-columnas.md` §4) |

Viene del diseño, no lo decide este playbook:

- Nombre, descripción, columnas y permisos (leer, crear, actualizar): `02` §8 e inventario §6.1. Origen: RNF-03, RNF-10, D-19, D-30, P-16.
- Quién lo recibe: el team del sistema de la business unit ARP (cuya membresía administra la plataforma) y el team ARP Jefatura (de grupo de Entra ID en BAC). Un perfil se da a usuarios o equipos, nunca a roles (`02` §8, inventario 13.2 y 13.3).
- Las tres columnas ya están marcadas `protegida: true` en los playbooks de su tabla: `sanic_cedula` en `playbooks/tabla/sanic_carp_tbl_cliente.md` (3.1) y `sanic_cuentadebitar` y `sanic_instantaneacedula` en `playbooks/tabla/sanic_carp_tbl_arreglo.md` (3.3). Ninguna otra columna del modelo está protegida, así que ninguna protegida queda sin cubrir.
- Seguridad de columna no es seguridad de registro: la cédula no viaja por otra columna ni por adjuntos de la solución; el archivo del formato generado nunca se guarda (`02` §4.7); los adjuntos de los correos pueden traer datos sensibles y se cubren con el aislamiento por business unit (D-18, `02` §8).
- Ninguna de las tres columnas es parte de una clave alternativa (K-01, K-02 y K-03 no las usan; DD-06).

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/seguridad/patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 3 | Las tablas existen | `sanic_carp_tbl_cliente` (3.1) y `sanic_carp_tbl_arreglo` (3.3), construidas |
| 4 | Cada columna existe y **tiene seguridad de columna** (`IsSecured = true`) | `sanic_cedula` · `sanic_cuentadebitar` · `sanic_instantaneacedula` |
| 5 | El nombre no lleva tildes ni ñ (BP-PP-197) y empieza con `CSP - CARP - ` | el de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/seguridad/patrones.md` §2.2 "Column security profile". Herramienta del proyecto: `herramientas/construir/perfil_columnas.py`. Crea el perfil y un permiso por columna; no le asigna miembros.

```
python3 herramientas/construir/perfil_columnas.py playbooks/perfil-columnas/csp_carp_datos_sensibles.md
```

## 5. Verificación

```
python3 herramientas/construir/perfil_columnas.py playbooks/perfil-columnas/csp_carp_datos_sensibles.md --solo-verificar
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_cliente.md --solo-verificar
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_arreglo.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET fieldsecurityprofiles` por nombre | un solo perfil · `name = CSP - CARP - Datos sensibles` · `ismanaged = false` · la descripción de la sección 2 |
| 2 | `GET fieldpermissions` del perfil | **exactamente tres**, y todos con `canreadunmasked = 0`: `sanic_carp_tbl_cliente.sanic_cedula` con `canread = 4` · `cancreate = 4` · `canupdate = 4`; `sanic_carp_tbl_arreglo.sanic_cuentadebitar` con `canread = 4` · `cancreate = 4` · `canupdate = 4`; `sanic_carp_tbl_arreglo.sanic_instantaneacedula` con `canread = 4` · `cancreate = 0` · `canupdate = 0`. Uno que falte, uno con otro valor y uno de más son `difiere` |
| 3 | Pertenencia a la solución | el perfil figura una vez en la solución de la sección 1 (`solutioncomponents`, tipo 70); sus permisos viajan dentro |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 5 | Las tablas siguen coincidiendo con sus propios playbooks | `ya_existia` en las dos |
| 6 | Comprobación independiente contra el XML exportado de la solución | los tres permisos con los valores de la fila 2, y el perfil sin miembros |

Los miembros **no** se verifican: no son parte de lo construido.

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. La herramienta nunca quita ni cambia un permiso. Si lo único que pasa es que falta un permiso (una corrida cortada), se completa con `--completar`; eso lo decide quien dirige la construcción, no el constructor.

## 7. Reversa

`DELETE fieldsecurityprofiles(<id>)`: se lleva sus permisos. Nada es irreversible. Pero agregar o quitar un permiso cambia en el momento lo que ven quienes ya reciben el perfil: sin perfil, una columna protegida solo la ve un administrador del sistema, y la búsqueda por cédula devuelve cero filas **sin error** (D-19).

## 8. Fuera de alcance

- **Asignación de miembros**: asociar el team del sistema de la business unit ARP y el team `Arreglos de Pago - Jefatura` a este perfil, y la prueba de punta a punta (entrar al grupo de Entra permite leer la cédula, salir deja de permitirlo): tarea de administración 13.8, en cada entorno. La asociación perfil-equipo no viaja en la solución. Los teams en sí: 13.2 y 13.3; el movimiento de usuarios a la business unit: 13.7. La prueba de que la pertenencia por team se comporta igual que por usuario sigue abierta (D-19, spike 4). El alta de cada usuario debe incluir su pertenencia al perfil.
- No hay otros perfiles previstos.
- Las tres columnas protegidas en sí: ya construidas con sus tablas, inventario 3.1 y 3.3.
- Los adjuntos de los correos, que pueden traer datos sensibles: se cubren con el aislamiento por business unit (D-18) y la verificación 13.9, no con este perfil.
- Las vistas y el quick find que dependen de la cédula (`sanic_cedula`): inventario 12.10; dependen de este perfil (6.1).
