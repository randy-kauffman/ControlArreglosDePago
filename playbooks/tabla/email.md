# Playbook: columnas-estandar · email

## 1. Identidad

```json
{
  "tipo_playbook": "columnas-estandar",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "3.8",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §5 (con las decisiones P-03 y P-15 de su §11). El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas y columnas sobre la tabla estándar `email`.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles van en español y sin tildes ni ñ (BP-PP-197), con el mismo texto en los dos idiomas. Las descripciones son prosa y llevan ortografía completa. Existe otro publisher con el prefijo `sanic` ("Sanic Corp", unique name `sanic`): el playbook fija el publisher por su unique name.

**Madurez.** Este tipo de playbook es una variante de la referencia de tabla (`tabla.md`, sección 6) con **0 ciclos completos**. Este es su primer uso: se construye solo, con revisor, y su resultado se anota en el registro de tropiezos de la skill. No se construye en lote.

## 2. Qué se crea

```json
{
  "tipo": "columnas-estandar",
  "tabla": "email",
  "columnas": [
    {
      "nombre": "sanic_tiemporespuesta",
      "displayname": {
        "1033": "Tiempo de respuesta (minutos)",
        "3082": "Tiempo de respuesta (minutos)"
      },
      "descripcion": "Minutos entre el primer correo del cliente que esperaba respuesta y el envío de esta respuesta (senton menos sinrespuestadesde). Vacío si no había nada pendiente, por ejemplo en un recordatorio. Sostiene el promedio por respuesta y por oficial.",
      "tipo": "entero",
      "requerida": false,
      "protegida": false,
      "auditoria": false,
      "minimo": 0,
      "maximo": 2147483647
    }
  ]
}
```

### Cuenta de columnas

El diccionario (§5) trae **2 columnas propias** sobre `email`: `sanic_tiemporespuesta` (entero) y `sanic_respondidoporid` (lookup a `systemuser`). Este bloque trae **1**: la única que no es lookup. El lookup `sanic_respondidoporid` no va aquí: nace con su relación REL-19 (`relacion.md`, sección 6, tabla hija estándar).

### Decisiones que este playbook toma

Solo lo que el diccionario no trae. Nada de esto cambia un nombre lógico, un tipo, un rango, un requerida, una auditoría o una protección.

| # | Decisión | Motivo |
|---|---|---|
| 1 | La descripción es la del diccionario, **sin los acentos graves** (`senton − sinrespuestadesde` pasa a «senton menos sinrespuestadesde») y sin las referencias a RF y D-xx | Una descripción es lo que lee el usuario; el signo y los acentos graves no aportan y los acentos graves no conviene tenerlos dentro de un bloque json de un playbook |

### Viene del diseño (no es decisión de este playbook)

| Dato | Origen |
|---|---|
| Nombre lógico `sanic_tiemporespuesta`, tipo entero, rango `[0..2147483647]` (el máximo del tipo, sin tope arbitrario) | Diccionario §5, §11.3 (P-03) |
| Nombre visible `Tiempo de respuesta (minutos)`, con el mismo texto en 1033 y 3082 | Diccionario §5, P-17 y §0; `diseno/01-convenciones.md` §0 |
| `requerida: false`, `protegida: false`, `auditoria: false` | Diccionario §5: columnas `Req`, `Seg` y `Aud` en «No» |
| La columna es de solo-sistema (la escribe el plugin al pasar a Enviado) | Diccionario §5, columna `Sis`: no es un dato de este bloque; se aplica en el plugin (sección 8) |
| No se cambia la auditoría de `email` | Diccionario §5 («No cambian la auditoría de `email`») |

## 3. Precondiciones

Las de la variante (`tabla.md`, sección 6) y las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, el idioma base del entorno está en la lista y la lista incluye 1033 | `[1033, 3082]` |
| 3 | La tabla existe, es estándar y acepta columnas propias: `GET EntityDefinitions(LogicalName='email')?$select=IsCustomEntity,CanCreateAttributes,IsAuditEnabled` | 200 · `IsCustomEntity = false` · `CanCreateAttributes.Value = true` (se **lee**, no se supone: para una tabla de actividad como `email` no está comprobado) |
| 4 | Ninguna columna del bloque pide auditoría, así que la auditoría de `email` no es precondición | `auditoria: false` en `sanic_tiemporespuesta` |
| 5 | La tabla `email` no lleva el prefijo del publisher y el nombre de la columna sí | `email` · `sanic_tiemporespuesta` (21 caracteres) |
| 6 | Nombres visibles sin tildes ni ñ (BP-PP-197), una entrada por idioma | los de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2, variante de columnas sobre una tabla estándar: `POST EntityDefinitions(LogicalName='email')/Attributes` por columna, con la cabecera `MSCRM.SolutionUniqueName`, y `PublishXml` de la tabla una vez. La tabla `email` no se crea ni se modifica.

```
python3 herramientas/construir/tabla.py playbooks/tabla/email.md
```

La herramienta puede devolver `bloqueado` por «características todavía no verificadas contra la plataforma» (`variante:columnas-estandar` y `variante:pertenencia`): la variante tiene 0 ciclos. En ese caso el constructor se detiene y reporta el texto exacto; no usa `--permitir-no-verificadas` ni ningún otro flag. Fijar el criterio de la primera vez lo resuelve el arquitecto.

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/email.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | La tabla existe, es estándar y acepta columnas propias | 200 · `IsCustomEntity = false` · `CanCreateAttributes.Value = true` |
| 2 | `sanic_tiemporespuesta` | entero (`IntegerType`) · mínimo 0 · máximo 2147483647 · no requerida (`None`) · no protegida · `IsAuditEnabled = false` · nombre visible `Tiempo de respuesta (minutos)` en 1033 y en 3082, ninguna etiqueta en otro idioma · descripción la de la sección 2 |
| 3 | Ninguna columna propia de más | ninguna columna con `IsCustomAttribute = true` y prefijo `sanic` fuera de `sanic_tiemporespuesta`, salvo lookups (`sanic_respondidoporid`, de su relación REL-19) y las columnas sombra (`AttributeOf`) |
| 4 | Pertenencia a la solución | **Pendiente de verificar** (primer ciclo de la variante): lo esperado es que `email` figure en la solución con `rootcomponentbehavior` distinto de 0, y que se registren las filas de `solutioncomponents` de la columna y de la tabla. El playbook no puede declararse verificado en este punto hasta que un ciclo fije el criterio; la herramienta observa e informa lo que ve |
| 5 | Segunda ejecución de `tabla.py` sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5 (1 a 4; en una tabla estándar no se buscan «columnas de más» entre cientos, solo las propias del publisher). Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**: una columna que difiere no se toca, se informa.

## 7. Reversa

Irreversibles desde que se crea: el nombre lógico `sanic_tiemporespuesta` y su tipo (entero). Borrar la columna (`DELETE EntityDefinitions(LogicalName='email')/Attributes(LogicalName='sanic_tiemporespuesta')`) borra sus datos. La tabla estándar `email` nunca se borra ni se modifica desde este playbook. Se puede cambiar después: nombre visible, descripción, requerida y auditoría.

## 8. Fuera de alcance

- El lookup `sanic_respondidoporid` (Respondido por, `systemuser`, opcional): nace con la relación REL-19, variante de `relacion.md` cuya tabla hija es una tabla estándar (inventario de relaciones).
- La auditoría y las demás propiedades de la tabla `email`, que este playbook no cambia.
- Los plugins que escriben las columnas: `sanic_tiemporespuesta` es de solo-sistema (la escribe el plugin al pasar a Enviado) y `sanic_respondidoporid` también.
- Los formularios y las vistas donde aparecen las columnas (la vista de promedio por respuesta y por oficial).
