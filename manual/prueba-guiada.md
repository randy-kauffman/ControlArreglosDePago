# Prueba guiada: Control de Arreglos de Pago (Dev)

Recorrido completo de la aplicación en Dev, de punta a punta, con dos usuarios reales: el **Oficial** y el **Jefe**. Cada paso dice quién lo hace, dónde, qué escribir y qué tiene que pasar. Marque cada paso con OK o Falla y anote lo que vea.

Todo lo que se carga es ficticio (prefijo `HUMO-GUIA`). No use datos de clientes reales.

---

## Antes de empezar

### Requisitos

| Qué | Detalle |
|---|---|
| Ventana A, el **Oficial** | `User8@55xljh.onmicrosoft.com` (# User 8): rol Oficial por el team del sistema de la BU |
| Ventana B, el **Jefe** | `User9@55xljh.onmicrosoft.com` (# User 9): Oficial + Jefe por el team Jefatura |
| Correo del "cliente" | Outlook web con el **buzón de prueba 3** (`PP_MAIL_BUZON_3`, `buzondevpp3@55xljh.onmicrosoft.com`) |
| Buzón del área | **Buzón de prueba 1** (`PP_MAIL_BUZON_1`, `buzondevpp1@55xljh.onmicrosoft.com`), conectado a la cola "Arreglos de Pago" |
| App | `https://org36e60d9d.crm.dynamics.com/main.aspx?appid=f13895ff-9bcc-f111-a05b-00224808ffcd` |
| Excel de escritorio | Para abrir los formatos descargados |

**¿Por qué el buzón 3 y no el 2?** El buzón 2 ya tiene un cliente de prueba registrado con su correo (de la tanda 3). Un correo desde el buzón 2 se vincula solo a ese cliente, y entonces no se puede probar "crear y vincular el cliente". El buzón 3 no tiene cliente: es el caso real de un cliente nuevo. El buzón 2 se usa solo en la etapa 1b, para ver la vinculación automática.

**No use `admin@55xljh.onmicrosoft.com` para probar.** Es administrador del sistema: se salta todas las reglas de seguridad y nunca va a ver un error de permisos, aunque lo haya.

**No escriba nunca a otra dirección** que no sea la del buzón del área. Las respuestas salen de la aplicación solo hacia el buzón 3 (o el 2).

### Tiempo estimado

Unas **2 horas y media**. Las esperas de correo son de 30 segundos a 2 minutos cada una.

### Cómo reportar una falla

1. **Primero recargue con Ctrl+F5** y repita el paso. Muchas fallas son caché del navegador.
2. Si sigue fallando, anote:
   - el **paso** (por ejemplo 6.4);
   - el **código** si el mensaje empieza con `CARP-...`;
   - si no hay código, el botón **Detalles** o **Descargar archivo de registro** del mensaje: copie el **Activity Id**;
   - la **hora** aproximada;
   - una **captura** de la pantalla completa.
3. Marque el paso como Falla y siga con el siguiente si no depende de él.

### Datos de prueba, listos para copiar

| Dato | Valor |
|---|---|
| Nombre completo del cliente | `HUMO GUIA Cliente Uno` |
| Cédula | `999-281026-0001H` (si el sistema dice `CARP-CLI-003`, ya existe: cambie los últimos 4 dígitos) |
| Correo del cliente | `buzondevpp3@55xljh.onmicrosoft.com` |
| Celular | `+505 8888 0001` |
| Producto crediticio | `Prestamo` |
| Número de préstamo o tarjeta (arreglo 1) | `HUMOGUIA0001` |
| Número de préstamo o tarjeta (arreglo 2) | `HUMOGUIA0002` |
| Tipo de arreglo | `Temporal` |
| Moneda | `Cordobas` |
| Canal de origen | `Correo` (se llena solo si el arreglo se crea desde la conversación) |
| Cuenta a debitar | `HUMO-0000000001` |
| Saldo inicial | `30000.00` |
| Monto abonado | `6000.00` |
| Plazo en cuotas | `6` |
| Cuota mensual | `4500.00` |
| Fecha de inicio | `31/01/2027` |

Con estos montos el sistema tiene que calcular:

| Resultado | Valor esperado | Por qué |
|---|---|---|
| Saldo remanente | `24,000.00` | 30,000 − 6,000 |
| Total a pagar | `24,000.00` | 6 × 4,500 = 27,000 pasa el saldo, entonces el total es el saldo |
| Cuotas 1 a 5 | `4,500.00` cada una | |
| Cuota 6 | `1,500.00` | 24,000 − 5 × 4,500 |
| Vencimientos | 31/01/2027, **28/02/2027**, 31/03/2027, **30/04/2027**, 31/05/2027, **30/06/2027** | Mismo día de cada mes desde el inicio; si el mes no tiene día 31, el último del mes |
| Fecha de fin | `30/06/2027` | Vencimiento de la última cuota |

---

## Etapa 0: preparación

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 0.1 | Vos | Abra **dos ventanas privadas de navegadores distintos** (por ejemplo, Chrome incógnito y Edge InPrivate) | Dos sesiones que no comparten cookies | [ ] OK [ ] Falla |
| 0.2 | A | Inicie sesión como `User8@55xljh.onmicrosoft.com` y abra la URL de la app | Se abre **Control de Arreglos de Pago** | [ ] OK [ ] Falla |
| 0.3 | B | Inicie sesión como `User9@55xljh.onmicrosoft.com` y abra la URL de la app | Se abre la misma app | [ ] OK [ ] Falla |
| 0.4 | A | Mire el menú de la izquierda | Dos áreas: **Conversaciones** (5 entradas) y **Clientes y arreglos** (8 entradas). **No** aparecen "Supervisión" ni "Administración del formato" | [ ] OK [ ] Falla |
| 0.5 | B | Mire el menú de la izquierda | Además de las dos anteriores: **Supervisión** (6 entradas, la última es **Panel de supervisión**) y **Administración del formato** (**Versiones de plantilla**). 20 entradas en total | [ ] OK [ ] Falla |
| 0.6 | Vos | En otra pestaña, abra Outlook web con el **buzón de prueba 3** | Ve la bandeja del buzón 3 | [ ] OK [ ] Falla |

Notas:

---

## Etapa 1: llega el correo del cliente

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 1.1 | Outlook (buzón 3) | Escriba un correo nuevo **a** `buzondevpp1@55xljh.onmicrosoft.com` con el asunto y el cuerpo de abajo. Envíelo y anote la hora | Sale sin error | [ ] OK [ ] Falla |
| 1.2 | A | Espere **1 minuto**. Vaya a **Conversaciones → Conversaciones sin asignar** y actualice la lista | Aparece una conversación `CONV-00xxxx` con el asunto `HUMO-GUIA-01 Solicitud de arreglo de pago`. Anote el número: ____________ | [ ] OK [ ] Falla |
| 1.3 | A | Ábrala | Estado **Abierta**, **Oficial asignado** vacío, **Cliente** vacío, el remitente es el buzón 3. En **Correos** (la línea de tiempo) está el correo del cliente | [ ] OK [ ] Falla |

**Asunto:**
```
HUMO-GUIA-01 Solicitud de arreglo de pago
```

**Cuerpo:**
```
Buenos días.

Tengo atrasadas dos cuotas de mi préstamo HUMOGUIA0001 y quisiera un arreglo de pago.
Puedo abonar 6,000 córdobas ahora y pagar el resto en 6 cuotas mensuales.

Quedo atento.
HUMO GUIA Cliente Uno
(correo de prueba, datos ficticios)
```

### 1b (opcional): vinculación automática con un cliente que ya existe

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 1b.1 | Outlook (buzón 2) | Si tiene acceso al buzón 2, envíe al buzón del área un correo con el asunto `HUMO-GUIA-1B Consulta de saldo` | Sale sin error | [ ] OK [ ] Falla [ ] No aplica |
| 1b.2 | A | A 1 minuto, búsquelo en **Conversaciones sin asignar** y ábralo | La conversación nace **ya con Cliente** (el cliente de prueba de la tanda 3, cuyo correo es el del buzón 2) | [ ] OK [ ] Falla [ ] No aplica |

Notas:

---

## Etapa 2: el Oficial toma la conversación

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 2.1 | A | En la conversación `HUMO-GUIA-01`, pulse **Tomar** en la barra de comandos | Aviso "Conversación tomada por # User 8." **Oficial asignado** = # User 8. El botón **Tomar** desaparece | [ ] OK [ ] Falla |
| 2.2 | A | Vaya a **Conversaciones → Mis conversaciones pendientes** | Aparece la conversación | [ ] OK [ ] Falla |
| 2.3 | B | Abra la misma conversación desde **Conversaciones → Conversaciones abiertas** | Ve **Oficial asignado** = # User 8. **Tomar** no aparece (ya tiene oficial) | [ ] OK [ ] Falla |
| 2.4 | A | En **Conversaciones sin asignar**, seleccione **dos** filas y pulse **Tomar** (si no hay dos sin asignar, salte el paso) | Mensaje "Seleccione una sola conversación." y no toma nada | [ ] OK [ ] Falla [ ] No aplica |

Notas:

---

## Etapa 3: el Oficial crea y vincula el cliente

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 3.1 | A | En la conversación, en el campo **Cliente**, escriba `HUMO GUIA` | No encuentra a nadie con ese nombre | [ ] OK [ ] Falla |
| 3.2 | A | Use **+ Nuevo** dentro del mismo campo. Llene **Nombre completo**, **Cedula**, **Correo** y **Celular** con los datos de la tabla, y guarde | Se crea el cliente. Su **Nombre** es una copia del nombre completo | [ ] OK [ ] Falla |
| 3.3 | A | De vuelta en la conversación, confirme que **Cliente** = `HUMO GUIA Cliente Uno` y guarde | Guarda sin error | [ ] OK [ ] Falla |
| 3.4 | A | **Error esperado.** Cree otro cliente desde **Clientes y arreglos → Clientes → + Nuevo** con la **misma cédula** y otro nombre (`HUMO GUIA Duplicado`) | `CARP-CLI-003` "Ya existe un cliente con esa cédula." No se crea | [ ] OK [ ] Falla |
| 3.5 | A | **Error esperado.** En el mismo formulario nuevo, deje **Cedula** vacía e intente guardar | La plataforma pide el campo obligatorio, o `CARP-CLI-002`. No se crea. Descarte el formulario sin guardar | [ ] OK [ ] Falla |
| 3.6 | A | Abra el cliente `HUMO GUIA Cliente Uno` | Ve la **Cedula** con su valor (el Oficial tiene el permiso de datos sensibles por su team) | [ ] OK [ ] Falla |

Notas:

---

## Etapa 4: el Oficial responde y el cliente contesta

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 4.1 | A | En **Correos** de la conversación, abra el correo del cliente y pulse **Responder** | Se abre el borrador. El **De** muestra la cola "Arreglos de Pago" o su usuario (el sistema lo cambia por la cola al guardar o enviar) | [ ] OK [ ] Falla |
| 4.2 | A | Escriba el cuerpo de abajo y pulse **Enviar**. Anote la hora | El correo pasa a **Enviado** en menos de 1 minuto | [ ] OK [ ] Falla |
| 4.3 | Outlook (buzón 3) | Espere 1 o 2 minutos y revise la bandeja | Llega la respuesta con remitente **buzón del área** (`buzondevpp1@...`), **no** User 8 | [ ] OK [ ] Falla |
| 4.4 | Outlook (buzón 3) | Pulse **Responder** sobre ese correo (en el mismo hilo), escriba `Acepto las condiciones. HUMO-GUIA` y envíe | Sale sin error | [ ] OK [ ] Falla |
| 4.5 | A | Espere 2 minutos y actualice la conversación | La respuesta del cliente aparece en **la misma** conversación. **No** se crea otra conversación | [ ] OK [ ] Falla |
| 4.6 | A | Mire la sección **Tiempos** | **Tiempo hasta la primera respuesta** tiene un valor en minutos y **Cantidad de respuestas** = 1 | [ ] OK [ ] Falla |

**Cuerpo de la respuesta del Oficial:**
```
Estimado cliente:

Recibimos su solicitud. Le proponemos un abono de C$ 6,000.00 y 6 cuotas mensuales
de C$ 4,500.00, la última de C$ 1,500.00, a partir del 31/01/2027.
Si está de acuerdo, responda este correo y le enviamos el formato para firmar.

Saludos,
Área de Arreglos de Pago
(correo de prueba, datos ficticios)
```

Notas:

---

## Etapa 5: seguridad, reasignar la conversación

Reasignar no tiene botón: se cambia el campo **Oficial asignado** y se guarda. Solo la jefatura puede.

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 5.1 | A | En la conversación, cambie **Oficial asignado** a # User 9 y guarde | `CARP-CNV-008` "Solo la jefatura puede reasignar una conversación o dejarla sin asignar." No cambia. Descarte el cambio (o recargue con F5) | [ ] OK [ ] Falla |
| 5.2 | A | Vacíe **Oficial asignado** y guarde | El mismo `CARP-CNV-008`. Recargue | [ ] OK [ ] Falla |
| 5.3 | B | En la misma conversación, cambie **Oficial asignado** a # User 9 y guarde | Guarda sin error | [ ] OK [ ] Falla |
| 5.4 | B | Vuelva a poner **Oficial asignado** = # User 8 y guarde | Guarda sin error. La conversación vuelve a ser del Oficial | [ ] OK [ ] Falla |

Notas:

---

## Etapa 6: el Oficial arma el arreglo en Borrador

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 6.1 | A | En la conversación, en la sección **Arreglos de la conversación**, pulse **+ Nuevo** | Se abre el arreglo con **Cliente** y **Conversacion** ya llenos | [ ] OK [ ] Falla |
| 6.2 | A | **Error esperado.** Pestaña **General**: llene todo con los datos de la tabla, **salvo Monto abonado = `40000.00`**. Guarde | `CARP-ARR-004` "El monto abonado no puede ser mayor que el saldo inicial." | [ ] OK [ ] Falla |
| 6.3 | A | **Error esperado.** Ponga **Monto abonado** = `6000.00` y **Plazo en cuotas** = `7`. Guarde | `CARP-ARR-005` "Con una cuota menos ya se cubre el saldo remanente. El saldo se cubre con 6 cuotas." | [ ] OK [ ] Falla |
| 6.4 | A | Ponga **Plazo en cuotas** = `6` y guarde | Guarda. Estado **Borrador**. **Código interno** `ARR-00xxxx` y un **Número provisional**. **Version de plantilla** = v07. Anote el código: ____________ | [ ] OK [ ] Falla |
| 6.5 | A | Revise **Montos y plazo** | Saldo remanente `24,000.00`, Total a pagar `24,000.00`, Fecha de fin `30/06/2027`. **Canal de origen** = Correo | [ ] OK [ ] Falla |
| 6.6 | A | Pestaña **Cuotas** | 6 cuotas: cinco de `4,500.00` y la sexta de `1,500.00`, con los vencimientos de la tabla (28/02 y 30/04 son los casos de fin de mes) | [ ] OK [ ] Falla |
| 6.7 | A | **Error esperado.** En **Cuotas**, abra la cuota 1, llene **Fecha de pago** y **Monto pagado**, y guarde | `CARP-CUO-002` "Solo se pueden registrar pagos en un arreglo vigente." Descarte el cambio | [ ] OK [ ] Falla |
| 6.8 | A | **Error esperado.** Vuelva al arreglo. Cambie **Cuota mensual** a `4800.00` y guarde | `CARP-ARR-005` "... El saldo se cubre con 5 cuotas." (4,800 × 5 = 24,000 ya cubre el saldo). Si guarda sin error, es una falla. Vuelva a `4500.00` o recargue | [ ] OK [ ] Falla |

Notas:

---

## Etapa 7: generar el formato, congelar y desbloquear

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 7.1 | A | En el arreglo, con todo guardado, pulse **Generar formato** | Pide confirmación y avisa que **se congelan** los datos del acuerdo y del cliente | [ ] OK [ ] Falla |
| 7.2 | A | Confirme | "Generando…" y se descarga `F-NIC-0000052...xlsx`. Sin advertencias | [ ] OK [ ] Falla |
| 7.3 | Vos | Abra el `.xlsx` en Excel de escritorio | **No** pide reparar. Están el nombre, la cédula, el producto `HUMOGUIA0001`, los montos y las 6 cuotas con sus fechas | [ ] OK [ ] Falla |
| 7.4 | A | Vuelva al arreglo y actualice | En el encabezado hay **Fecha de congelamiento**. La sección **Cliente al congelar** tiene nombre, cédula, correo y celular. En **Formatos** hay una fila | [ ] OK [ ] Falla |
| 7.5 | A | **Error esperado.** Cambie **Cuota mensual** a `4600.00` y guarde | `CARP-ARR-011` "El arreglo está congelado: no se puede modificar ...". Recargue | [ ] OK [ ] Falla |
| 7.6 | A | Pulse **Desbloquear** y confirme | Se borra la **Fecha de congelamiento**. Avisa que el formato impreso deja de valer | [ ] OK [ ] Falla |
| 7.7 | A | **Error esperado.** Pulse **Desbloquear** de nuevo, si el botón sigue visible | `CARP-ARR-017` "El arreglo no está congelado ...", o el botón ya no aparece (las dos cosas son correctas) | [ ] OK [ ] Falla |
| 7.8 | A | Pulse **Generar formato** otra vez y confirme | Vuelve a congelar y descarga otro `.xlsx`. En **Formatos** hay 2 filas | [ ] OK [ ] Falla |

Notas:

---

## Etapa 8: formalizar, el arreglo pasa a Vigente

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 8.1 | A | Pulse **Formalizar** | Pide confirmar que **el cliente firmó** el formato impreso | [ ] OK [ ] Falla |
| 8.2 | A | Confirme | El arreglo pasa a **Vigente** y el mensaje dice el **número definitivo**. Anótelo: ____________ | [ ] OK [ ] Falla |
| 8.3 | A | Mire la barra de comandos | Ya no están **Desbloquear**, **Formalizar** ni **Descartar**. Está **Anular** | [ ] OK [ ] Falla |
| 8.4 | A | Vaya a **Clientes y arreglos → Arreglos vigentes** y a **Arreglos de préstamo** | El arreglo aparece en las dos | [ ] OK [ ] Falla |
| 8.5 | A | **Error esperado.** Cambie **Plazo en cuotas** y guarde | `CARP-ARR-011` (congelado). Recargue | [ ] OK [ ] Falla |

Notas:

---

## Etapa 9: registrar el pago de una cuota

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 9.1 | A | **Error esperado.** Pestaña **Cuotas** → abra la cuota 1. Llene **solo Fecha de pago** = hoy y guarde | `CARP-CUO-003` "El pago de una cuota se registra con su fecha y su monto juntos ...". Recargue | [ ] OK [ ] Falla |
| 9.2 | A | **Error esperado.** Cambie el **Monto** (el de la cuota, no el pagado) y guarde | `CARP-CUO-001` "En una cuota solo se puede registrar la fecha y el monto de pago ...", o el campo está bloqueado. Recargue | [ ] OK [ ] Falla |
| 9.3 | A | Llene **Fecha de pago** = hoy y **Monto pagado** = `4500.00`. Guarde | Guarda sin error | [ ] OK [ ] Falla |
| 9.4 | A | Vuelva al arreglo y pulse **Generar formato** | Como está Vigente, **no** pide congelar: genera y descarga. El arreglo no cambia | [ ] OK [ ] Falla |
| 9.5 | Vos | Abra ese `.xlsx`, hoja **CONTROL DE PAGOS** | En **D7** está la fecha del pago **con formato de fecha** (dd/mm/aaaa, no un número) y el monto `4,500.00` | [ ] OK [ ] Falla |

Notas:

---

## Etapa 10: campos de gesto, finalizar la conversación y sus tiempos

Los campos **Resultado**, **Motivo** y **Detalle** de la conversación, y los motivos del arreglo, solo se guardan con su botón.

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 10.1 | A | **Bloqueo esperado.** En la conversación, sección **Finalización**, elija **Resultado** = `Con arreglo de pago` y pulse **Guardar** (no Finalizar) | La aplicación **no guarda** y avisa que se usa el botón **Finalizar**. Recargue | [ ] OK [ ] Falla |
| 10.2 | A | **Error esperado.** Con **Resultado** vacío, pulse **Finalizar** y confirme | `CARP-CNV-003` "Para finalizar la conversación hay que indicar el resultado." | [ ] OK [ ] Falla |
| 10.3 | A | **Error esperado.** Elija **Resultado** = `Sin respuesta`, sin motivo ni detalle, y pulse **Finalizar** | `CARP-CNV-004` "Si el resultado es "Sin respuesta", el motivo y el detalle son obligatorios." | [ ] OK [ ] Falla |
| 10.4 | A | Elija **Resultado** = `Con arreglo de pago` y pulse **Finalizar**. Confirme | Estado **Finalizada**. **Finalizado por** = # User 8 y la fecha | [ ] OK [ ] Falla |
| 10.5 | A | Mire **Tiempos** | **Tiempo total** tiene valor en minutos. **Tiempo hasta la primera respuesta** sigue igual que en 4.6 | [ ] OK [ ] Falla |
| 10.6 | A | **Error esperado.** En la conversación finalizada, cambie **Cliente** y guarde | `CARP-CNV-009` "Una conversación finalizada o unida no se puede modificar ...". Recargue | [ ] OK [ ] Falla |
| 10.7 | A | Vaya a **Conversaciones → Conversaciones finalizadas** | Aparece la conversación con sus tiempos | [ ] OK [ ] Falla |

Notas:

---

## Etapa 11: el cliente vuelve a escribir, iniciar conversación nueva y unir

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 11.1 | Outlook (buzón 3) | **Responda en el mismo hilo** a la última respuesta del área: `Tengo una consulta nueva sobre mi tarjeta. HUMO-GUIA` | Sale sin error | [ ] OK [ ] Falla |
| 11.2 | A | A los 2 minutos, vaya a **Conversaciones → Correos llegados a conversaciones finalizadas** | Aparece la conversación `HUMO-GUIA-01`, todavía **Finalizada**, con la marca **Correo nuevo en finalizada** | [ ] OK [ ] Falla |
| 11.3 | A | Ábrala y pulse **Iniciar conversación nueva**. Confirme | Se abre una **conversación nueva**, Abierta, con el correo nuevo. La anterior sigue Finalizada y pierde la marca. Anote el número nuevo: ____________ | [ ] OK [ ] Falla |
| 11.4 | A | **Error esperado.** Vuelva a la conversación anterior. Si el botón **Iniciar conversación nueva** sigue visible, púlselo | `CARP-INI-003` "Solo una conversación finalizada con correos nuevos ...", o el botón ya no aparece | [ ] OK [ ] Falla |
| 11.5 | Outlook (buzón 3) | Envíe un correo **nuevo** (no respuesta) al buzón del área con el asunto `HUMO-GUIA-02 Otra consulta` y cuerpo `Segunda consulta de prueba.` | Sale sin error | [ ] OK [ ] Falla |
| 11.6 | A | A 1 minuto, abra la conversación `HUMO-GUIA-02` desde **Conversaciones sin asignar** | Nace **con Cliente** = `HUMO GUIA Cliente Uno` (coincide el correo). Arriba aparece el **aviso de otra conversación abierta** con el número de la conversación de 11.3 | [ ] OK [ ] Falla |
| 11.7 | A | Pulse **Tomar** | Queda asignada a # User 8 | [ ] OK [ ] Falla |
| 11.8 | A | Pulse **Unir a otra conversación**, elija como destino la conversación de 11.3 y confirme | Avisa que **no se puede deshacer**. Al confirmar abre la conversación de destino e informa cuántos correos se movieron (1) | [ ] OK [ ] Falla |
| 11.9 | A | Abra `HUMO-GUIA-02` | Estado **Unida**. Sus correos están ahora en la de destino | [ ] OK [ ] Falla |
| 11.10 | A | Abra la de destino (11.3) y finalícela con **Resultado** = `Sin respuesta`, **Motivo** = `Otro`, **Detalle** = `Prueba guiada HUMO, sin gestión real.` | Estado **Finalizada** | [ ] OK [ ] Falla |
| 11.11 | A | En la conversación de 11.10, pulse **Reabrir** y confirme | Vuelve a **Abierta** y se limpian resultado, motivo y detalle | [ ] OK [ ] Falla |
| 11.12 | A | Finalícela otra vez igual que en 11.10 | **Finalizada** | [ ] OK [ ] Falla |

Notas:

---

## Etapa 12: arreglo 2, descartar, anular y reabrir

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 12.1 | A | Desde el cliente `HUMO GUIA Cliente Uno`, sección **Arreglos del cliente**, **+ Nuevo**. Mismos datos que el arreglo 1, pero **Número de préstamo o tarjeta** = `HUMOGUIA0002` y **Canal de origen** = `Telefono`. Guarde | Borrador nuevo | [ ] OK [ ] Falla |
| 12.2 | A | **Error esperado.** Pulse **Descartar** sin escribir motivo | `CARP-ARR-013` "Para descartar el borrador hay que indicar el motivo." | [ ] OK [ ] Falla |
| 12.3 | A | **Bloqueo esperado.** Pestaña **Cierre**, escriba **Motivo de descarte** = `Prueba guiada HUMO` y pulse **Guardar** (no Descartar) | La aplicación **no guarda** y pide usar el botón | [ ] OK [ ] Falla |
| 12.4 | A | Con el motivo escrito, pulse **Descartar** y confirme | Estado **Descartado**. Aparece en **Arreglos cerrados** | [ ] OK [ ] Falla |
| 12.5 | A | Cree el arreglo 3 igual que el 12.1 pero con **Número de préstamo o tarjeta** = `HUMOGUIA0003`. **Generar formato** y **Formalizar** | **Vigente** | [ ] OK [ ] Falla |
| 12.6 | A | **Error esperado.** Pulse **Anular** sin motivo | `CARP-ARR-014` "Para anular el arreglo hay que indicar el motivo." | [ ] OK [ ] Falla |
| 12.7 | A | En **Cierre**, **Motivo de anulación** = `Prueba guiada HUMO`, pulse **Anular** y confirme | Estado **Anulado**. Aparece en **Arreglos cerrados** | [ ] OK [ ] Falla |
| 12.8 | A | **Error esperado.** Abra el arreglo 1 (Vigente). Si ve **Reabrir arreglo**, escriba un motivo en **Cierre** y púlselo | `CARP-ARR-016` "Solo la jefatura puede reabrir un arreglo.", o el botón no aparece para el Oficial | [ ] OK [ ] Falla |
| 12.9 | B | Abra el arreglo 1. En **Cierre**, **Motivo de reapertura** = `Corrección solicitada por el cliente (HUMO)`. Pulse **Reabrir arreglo** y confirme | Se borra la **Fecha de congelamiento**. Sigue **Vigente** | [ ] OK [ ] Falla |
| 12.10 | A | En el arreglo 1, **Generar formato** | El mensaje avisa que **se vuelve a congelar** con los datos actuales. Descarga el `.xlsx` | [ ] OK [ ] Falla |

Notas:

---

## Etapa 13: supervisión del Jefe

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 13.1 | B | **Supervisión → Panel de supervisión** | Dos gráficos: **promedio de minutos por respuesta, por oficial** (aparece # User 8) y **tiempo total promedio por oficial**. Dos listas: **Correos sin respuesta** y **Respuestas con error de envío** | [ ] OK [ ] Falla |
| 13.2 | B | **Supervisión → Respuestas enviadas** | Está la respuesta de la etapa 4, con quién respondió (# User 8) y su tiempo | [ ] OK [ ] Falla |
| 13.3 | B | **Supervisión → Finalizadas sin respuesta** | Está la conversación de 11.12 | [ ] OK [ ] Falla |
| 13.4 | B | Recorra **Correos sin respuesta**, **Respuestas con error de envío** y **Borradores sin movimiento** | Abren sin error (pueden estar vacías) | [ ] OK [ ] Falla |
| 13.5 | B | Abra el arreglo 1 y vaya a la pestaña de **auditoría** (historial) | Se ve quién cambió qué y cuándo: congelamiento, formalización, reapertura. **No** aparecen montos ni la cédula | [ ] OK [ ] Falla |
| 13.6 | A | Abra el mismo arreglo 1 y su **auditoría** | El Oficial **también** ve el historial | [ ] OK [ ] Falla |
| 13.7 | A | En la barra de direcciones de la ventana A, cambie el final de la URL por la del panel (copie la de la ventana B) | El Oficial **no** puede abrir el panel (mensaje de que no tiene acceso al panel o no se encuentra) | [ ] OK [ ] Falla |

Notas:

---

## Etapa 14: plantilla del formato (solo el Jefe)

**Importante:** en esta etapa **no** publique ninguna versión. La v07 vigente es la aprobada por el área de negocio. Todo lo que se prueba acá son validaciones que **rechazan** la publicación.

| # | Quién | Qué hacer | Resultado esperado | Resultado |
|---|---|---|---|---|
| 14.1 | B | **Administración del formato → Versiones de plantilla** | Aparece **v07** en estado **Vigente**, con su **Huella**, **Fecha de aprobación** y **Aprobado por** | [ ] OK [ ] Falla |
| 14.2 | B | Abra la v07 | La barra **no** tiene **Publicar versión** ni **Vista previa** (solo aparecen en un Borrador) | [ ] OK [ ] Falla |
| 14.3 | B | **Error esperado.** En la v07, cambie **Capacidad de cuotas** y guarde | `CARP-VER-003` "Solo una versión en Borrador se puede modificar ...". Recargue | [ ] OK [ ] Falla |
| 14.4 | B | **Error esperado.** **+ Nuevo**. **Numero de version** = `96` (sin la v), **Capacidad de cuotas** = `60`. Guarde | `CARP-VER-012` "El número de versión debe tener el formato v07 ..." | [ ] OK [ ] Falla |
| 14.5 | B | Cambie a **Numero de version** = `v96` y guarde | Se crea en **Borrador** | [ ] OK [ ] Falla |
| 14.6 | B | **Error esperado.** Sin subir archivo, pulse **Vista previa** y elija el arreglo 1 | `CARP-GEN-003` "La versión de plantilla no tiene archivo cargado." | [ ] OK [ ] Falla |
| 14.7 | B | **Error esperado.** Sin subir archivo, pulse **Publicar versión** y confirme | `CARP-VER-005` "La versión no tiene archivo de plantilla cargado." La v96 sigue en Borrador y la v07 sigue Vigente | [ ] OK [ ] Falla |
| 14.8 | A | En la ventana del Oficial, intente abrir **Versiones de plantilla** | La entrada **no** está en el menú del Oficial | [ ] OK [ ] Falla |

La v96 queda en Borrador como dato de prueba. No hace daño: nunca se usa para generar formatos mientras no se publique.

Notas:

---

## Resumen de resultados

| Etapa | Tema | Pasos | OK | Falla | No aplica |
|---|---|---|---|---|---|
| 0 | Preparación y menús | 6 | | | |
| 1 | Correo entrante (y 1b) | 3 (+2) | | | |
| 2 | Tomar | 4 | | | |
| 3 | Cliente | 6 | | | |
| 4 | Responder y respuesta del cliente | 6 | | | |
| 5 | Reasignar | 4 | | | |
| 6 | Arreglo en Borrador | 8 | | | |
| 7 | Formato, congelar, desbloquear | 8 | | | |
| 8 | Formalizar | 5 | | | |
| 9 | Pago de cuota | 5 | | | |
| 10 | Gestos y finalizar | 7 | | | |
| 11 | Iniciar nueva, unir, reabrir | 12 | | | |
| 12 | Descartar, anular, reabrir arreglo | 10 | | | |
| 13 | Supervisión | 7 | | | |
| 14 | Plantilla | 8 | | | |
| **Total** | | **99 (+2)** | | | |

### Fallas encontradas

| Paso | Código o Activity Id | Hora | Qué pasó |
|---|---|---|---|
| | | | |
| | | | |
| | | | |

### Correos enviados durante la prueba

Para control (todos entre buzones de prueba):

| Paso | De | A | Asunto |
|---|---|---|---|
| 1.1 | Buzón 3 | Buzón del área (1) | `HUMO-GUIA-01 Solicitud de arreglo de pago` |
| 1b.1 | Buzón 2 | Buzón del área (1) | `HUMO-GUIA-1B Consulta de saldo` (opcional) |
| 4.2 | Buzón del área (1) | Buzón 3 | `RE: HUMO-GUIA-01 ...` (respuesta del Oficial) |
| 4.4 | Buzón 3 | Buzón del área (1) | `RE: RE: HUMO-GUIA-01 ...` |
| 11.1 | Buzón 3 | Buzón del área (1) | `RE: ... HUMO-GUIA-01 ...` (a la finalizada) |
| 11.5 | Buzón 3 | Buzón del área (1) | `HUMO-GUIA-02 Otra consulta` |
