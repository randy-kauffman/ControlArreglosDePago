# Definición: Control de Arreglos de Pago

<!-- Plantilla del documento de definición (etapa 2). La completa el orquestador (skill `definicion`); los agentes arquitecto-solucion, revisor-arquitectura, estimador y redactor-audiencia no escriben archivos, solo devuelven contenido en el formato de retorno definido en cada archivo de agente. -->

## 1. Metadatos

- id: 2026-003-control-arreglos-pago
- fecha: 2026-10-07
- tipo: interno
- panorama: bac-credomatic/arreglos-de-pago
- estado: aprobada
- ficha_actualizada: 2026-10-07
- validacion_decision: go-con-condiciones
- validacion_fecha: 2026-10-07

<!-- ficha_actualizada = el valor de `actualizado` leído de ficha-solicitud.md al arrancar la etapa 2 (paso A). validacion_decision y validacion_fecha = el `resultado` y la `fecha` de la sección 8 de validacion.md. Si cualquiera de los tres hermanos cambia después, este documento queda desactualizado y hay que reejecutar /definicion <ID>. -->

## 2. Objetivos y criterios de éxito

Objetivo: el área de Arreglos de Pago controla de punta a punta las solicitudes de arreglo que llegan por correo y los arreglos que formaliza, con seguimiento de respuestas, de cuotas y del historial por cliente, sin Excel disperso [fuente: ficha §4].

Criterios de éxito [fuente: ficha §6]:

1. El 100 % de los correos de solicitud de arreglo recibe respuesta. Línea base: hay correos que nunca se responden y hoy no se miden.
2. El tiempo de respuesta de cada correo es visible y medible. Línea base: unas 3 horas en promedio.
3. Cero quejas de clientes por correos de solicitud sin responder. Línea base: 2 o 3 por mes.
4. El jefe del área comprueba que todos los correos se respondieron sin revisarlos uno por uno.
5. El área cuenta con una lista de los arreglos en riesgo para revisarlos.
6. Un oficial busca por cédula o por número de préstamo o tarjeta y ve al instante todos los arreglos del cliente, vigentes e históricos, con su estado.

## 3. Alcance y fases

### Incluido

- Recepción de los correos que llegan al buzón compartido del área, agrupados en conversaciones de ida y vuelta; asignación a un oficial; respuesta al cliente desde la solución con archivos adjuntos e imágenes; finalización de la conversación con un resultado; y medición de tiempos por respuesta y total [fuente: ficha §7] [fuente: validación del mockup, 2026-10-08].
- Supervisión de los correos sin respuesta y de los tiempos por oficial [fuente: ficha §6].
- Registro de clientes y del arreglo con todos los datos del formato F-NIC-0000052, para préstamos y tarjetas, con su calendario de hasta 60 cuotas. El arreglo nace en borrador, se puede descartar y pasa a vigente al formalizarlo [fuente: ficha §5, §7] [fuente: validación del mockup, 2026-10-08].
- Filtro por producto crediticio (préstamo o tarjeta) en las vistas de arreglos [fuente: validación del mockup, 2026-10-08].
- Generación del formato F-NIC-0000052 en Excel a partir de una plantilla con mapeo celda → campo, editable sin código por el dueño del formato, para imprimir y firmar [fuente: ficha §7, §8] [fuente: elicitación].
- Registro manual de pagos por cuota, lista de arreglos en riesgo y anulación [fuente: ficha §7].
- Historial por cliente, con búsqueda por cédula o por número de préstamo o tarjeta [fuente: ficha §6, §7].

- Mockup funcional simulado (HTML/React con datos simulados, descartable) para que negocio valide la propuesta antes de los spikes [fuente: elicitación]. Entregado y validado por negocio el 2026-10-08 en su versión 3, que incorpora los cambios pedidos en la validación (`mockup/index.html`).

### Excluido

- Todo el flujo físico: la firma presencial del formato y el pago de las cuotas por parte del cliente [fuente: ficha §7].
- Migración de los Excel de arreglos existentes en SharePoint; el control arranca con los arreglos nuevos [fuente: ficha §7].
- Integración con el AS400: los saldos y los pagos se cargan a mano [fuente: ficha §8].
- Botón de descarga en PDF: alcanza con el Excel descargado, que el usuario puede guardar como PDF [fuente: elicitación].
- Gestión de cobro posterior a la anulación, que sigue por Cobranza [fuente: ficha §10].
- Guardar una copia del documento generado: se regenera desde los datos [fuente: elicitación].

### Fases

<!-- Una fase = un incremento entregable. Si la talla de factibilidad-tecnica.md es XL, se requieren al menos 2 fases, con el MVP en la primera. -->

| Fase | Objetivo | Entregables | Requisitos |
|---|---|---|---|
| F1 | Entregar la solución completa en un solo corte (decisión del usuario, 2026-10-07) | Mockup funcional simulado validado por negocio; app model-driven con conversaciones de correo sincronizadas (respuesta con adjuntos, unión, finalización), supervisión, clientes, arreglos en borrador y formalizados, cuotas, generación del formato en Excel, administración de plantilla y mapeo, pagos, lista de riesgo, anulación e historial | RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11, RF-12, RF-13, RF-14, RF-15, RF-16, RF-17, RNF-01, RNF-02, RNF-03, RNF-04, RNF-05, RNF-06, RNF-07, RNF-08, RNF-09, RNF-10, RNF-11 |

## 4. Cumplimiento de condiciones

<!-- Una fila por cada condición listada en la sección 6 de validacion.md (si esa sección tiene N condiciones, acá van al menos N filas). ID: C-NN (NN de dos o más dígitos, único). Si validacion.md no tenía condiciones (recomendación/decisión distinta de go-con-condiciones), reemplazar la tabla por la palabra "Ninguna". -->

| ID | Condición | Cómo se cumple | Sección |
|---|---|---|---|
| C-01 | Factibilidad técnica (RF-06): spike con la plantilla real que confirme el relleno del Excel desde la plantilla con mapeo celda → campo y hasta 60 cuotas; plantilla v07 con 60 filas y totales corregidos aprobada por Héctor Romero; decisión sobre el PDF | El PDF quedó resuelto: alcanza con el Excel descargado [fuente: elicitación]. Spike 1 (2026-10-08): el relleno con Open XML SDK cumple en local (60 cuotas, filas sobrantes, recálculo, formato conservado); borrador de v07 con sus cambios celda por celda en `spikes/plantilla/`. Spike 1b (2026-10-08): funciona dentro del sandbox de Dataverse con salida idéntica a la local. **El lado técnico de C-01 queda cumplido.** La v07 la aprobó Héctor Romero el 2026-10-09 (aprobación escrita en poder del usuario, confidencial). Falta: registrar qué decidió sobre sus siete observaciones y abrir un archivo generado en Excel de escritorio | 6, 7, 10 |
| C-02 | Riesgo (normativa y datos sensibles): Seguridad de la Información o Cumplimiento confirma por escrito los requisitos de la SIBOIF y del sigilo bancario, y que Dataverse está aprobado para datos "Sensible" | Consulta formal a Cumplimiento antes de cerrar el diseño de datos; los requisitos que surjan se incorporan a RNF-10 | 5, 6, 10 |
| C-03 | Riesgo (plazo): Deyanira Ramírez confirma por escrito, después de la estimación, si la fecha 2026-10-31 queda sujeta a ese resultado o si acuerda un recorte o fases | El usuario decidió una sola fase (2026-10-07). Después del checkpoint 3 se presenta la estimación a Deyanira para su confirmación escrita. El 2026-10-08 el usuario decidió avanzar con los spikes sin esperar esa confirmación. **Cerrada el 2026-10-09:** Deyanira Ramírez confirmó que la fecha se mantiene en 2026-10-31 (confirmación en poder del usuario, confidencial), sin recorte ni fases. La estimación de 53,5 jornadas contra unos 16 días hábiles restantes queda como riesgo materializado (sección 9) | 3, 7, 8 |
| C-04 | Riesgo (datos existentes): Héctor Romero se compromete por escrito a un plan para los Excel con datos "Sensible" que quedan en SharePoint | Fuera del alcance técnico; queda como dependencia con responsable Héctor Romero | 10 |

## 5. Requisitos

<!-- ID: RF-NN para requisitos funcionales, RNF-NN para no funcionales (NN de dos o más dígitos, únicos en todo el documento). Tipo: `funcional` para RF, `no funcional` para RNF. Origen: de dónde sale el requisito, por ejemplo "[fuente: ficha §6]" o "[fuente: elicitación]" — nunca vacío. -->

| ID | Tipo | Descripción | Origen |
|---|---|---|---|
| RF-01 | funcional | Capturar automáticamente cada correo que llega al buzón compartido del área, incluidos los de remitentes que no existen como clientes, con su fecha y hora de llegada, y registrarlo en una conversación: un correo que responde a otro del mismo hilo se agrega a esa conversación, y un correo nuevo abre una conversación nueva | [fuente: ficha §4, §5, §7, §9] [fuente: elicitación] [fuente: validación del mockup, 2026-10-08] |
| RF-02 | funcional | Asignar cada conversación a un oficial, con la asignación visible para todo el área | [fuente: ficha §5, §7] |
| RF-03 | funcional | Responder al cliente desde la solución (desde el buzón compartido, en el mismo hilo), con archivos adjuntos e imágenes en el texto, dejando registro de qué se respondió y cuándo. Medir el tiempo de cada respuesta (desde el primer correo del cliente que la esperaba), el de la primera respuesta y el tiempo total de la conversación (del primer correo a la finalización) | [fuente: ficha §4, §6] [fuente: elicitación] [fuente: validación del mockup, 2026-10-08] |
| RF-04 | funcional | Vista de supervisión con los correos de clientes sin respuesta (incluidos los que llegan a una conversación ya finalizada), su antigüedad y los tiempos por oficial: promedio por respuesta y tiempo total | [fuente: ficha §6] [fuente: validación del mockup, 2026-10-08] |
| RF-05 | funcional | Registrar clientes en una tabla propia de la solución (nombre, cédula, correo, celular), sin usar la tabla estándar de contactos | [fuente: ficha §5] [fuente: elicitación] |
| RF-06 | funcional | Registrar el arreglo con todos los datos del formato F-NIC-0000052: producto (préstamo o tarjeta), número de préstamo o tarjeta, tipo (Temporal o Cancelación), moneda (córdobas o dólares; un solo arreglo en una sola moneda), canal de origen (correo, teléfono, presencial o sucursal), saldo inicial, monto abonado, saldo remanente, plazo en cuotas (hasta 60), cuota mensual, total a pagar, fechas de inicio y fin, cuenta a debitar, oficial, fecha de formalización y comentarios, vinculado al cliente y a la conversación de origen | [fuente: ficha §5, §7] [fuente: elicitación] [fuente: decisión de negocio, 2026-10-09] |
| RF-07 | funcional | Asignar a cada arreglo un número consecutivo por crédito (cuántas veces ese préstamo o tarjeta tuvo arreglo), sin duplicados. El número se fija al formalizar: un borrador o un arreglo descartado no consumen número | [fuente: ficha §5] [fuente: validación del mockup, 2026-10-08] |
| RF-08 | funcional | Generar el calendario de cuotas del arreglo (hasta 60) y el formato F-NIC-0000052 en Excel a partir de la plantilla vigente y el mapeo celda → campo, para descargar e imprimir. El documento no se guarda: se regenera desde los datos | [fuente: ficha §7, §8] [fuente: elicitación] |
| RF-09 | funcional | Administrar la plantilla Excel del formato y su mapeo celda → campo (incluido el bloque repetido de cuotas) sin tocar código; solo el jefe del área puede hacerlo | [fuente: ficha §8] [fuente: elicitación] |
| RF-10 | funcional | Registrar a mano el pago de cada cuota (fecha y monto) | [fuente: ficha §5, §8] |
| RF-11 | funcional | Lista de arreglos en riesgo: arreglos vigentes (formalizados) con al menos una cuota cuya fecha de vencimiento ya pasó (desde el día siguiente) sin pago registrado; el criterio es solo por fecha | [fuente: ficha §6] [fuente: elicitación] |
| RF-12 | funcional | Anular un arreglo, dejando registro de quién, cuándo y por qué | [fuente: ficha §5, §7] |
| RF-13 | funcional | Buscar por cédula o por número de préstamo o tarjeta y ver el historial completo de arreglos del cliente, vigentes y terminados, con su estado | [fuente: ficha §6] |
| RF-14 | funcional | Unir a mano a una conversación abierta los correos que el cliente envió por separado sobre el mismo tema. La solución avisa cuando el cliente tiene otra conversación abierta, pero no une sola | [fuente: validación del mockup, 2026-10-08] |
| RF-15 | funcional | Finalizar a mano una conversación con un resultado: Con arreglo de pago, Sin arreglo de pago o Sin respuesta (este último con motivo: spam, respuesta automática, el cliente no volvió a escribir u otro). Si llega un correo a una conversación finalizada, el oficial decide si la reabre o si inicia una conversación nueva con ese correo; mientras no decide, el correo cuenta como sin respuesta | [fuente: validación del mockup, 2026-10-08] |
| RF-16 | funcional | El arreglo nace en borrador: se edita libremente, no tiene número ni cuenta para la lista de riesgo. Al imprimir el formato para firma, el borrador se bloquea con un número provisional; el oficial puede desbloquearlo para corregir antes de la firma. Cuando el cliente firma, el oficial lo formaliza con un botón y pasa a vigente. Un borrador que no se concreta se descarta con motivo | [fuente: validación del mockup, 2026-10-08] |
| RF-17 | funcional | Filtrar las vistas de arreglos por producto crediticio (préstamo o tarjeta) | [fuente: validación del mockup, 2026-10-08] |
| RNF-01 | no funcional | 4 usuarios (3 oficiales y el jefe), todos con Power Apps Premium per user | [fuente: panorama §3] |
| RNF-02 | no funcional | Volumen: unas 80 solicitudes y 60 arreglos por semana (unos 3.000 arreglos por año), con hasta 60 cuotas por arreglo | [fuente: ficha §5] |
| RNF-03 | no funcional | Los datos de clientes son "Sensible": se guardan solo en Dataverse, nunca en SharePoint ni OneDrive, y lo mismo aplica a la plantilla | [fuente: ficha §8] [fuente: panorama §7] |
| RNF-04 | no funcional | Acceso restringido al área de ARP, incluido el buzón y sus registros. Dos perfiles: oficial (opera solicitudes y arreglos) y jefe (además supervisa y administra la plantilla y el mapeo) | [fuente: ficha §2, §8] [fuente: elicitación] |
| RNF-05 | no funcional | Sin integración con el AS400 | [fuente: ficha §8] |
| RNF-06 | no funcional | Power Platform con Dataverse en los entornos Dev, Test y Prod compartidos de Nicaragua; automatizaciones propiedad de una cuenta de servicio licenciada; sin tablas restringidas de Dynamics 365 (Case, SLA y similares) | [fuente: panorama §3, §6] [fuente: factibilidad-tecnica §6] |
| RNF-07 | no funcional | Retención: los datos de un arreglo se conservan 2 años desde que el arreglo terminó | [fuente: elicitación] |
| RNF-08 | no funcional | Se acepta una demora de 5 a 15 minutos entre la llegada del correo al buzón y su aparición en la solución | [fuente: elicitación] [fuente: factibilidad-tecnica §4] |
| RNF-09 | no funcional | Entrega el 2026-10-31, sujeta a la confirmación de C-03 | [fuente: ficha §8] |
| RNF-10 | no funcional | Cumplir los requisitos de auditoría de accesos, retención y ubicación de datos que confirme Cumplimiento (SIBOIF, sigilo bancario) | [fuente: ficha §10] [fuente: validacion §6] |
| RNF-11 | no funcional | Los adjuntos e imágenes de los correos respetan el tamaño máximo y los tipos de archivo bloqueados del entorno (configuración de toda la organización; por defecto 5 MB por archivo medidos en Base64), que se acuerdan con el administrador | [fuente: validación del mockup, 2026-10-08] |

## 6. Arquitectura de la solución

solucion: C4

### Componentes y responsabilidades
El diseño mantiene los componentes de C4 evaluados en factibilidad. No agrega conectores ni licencias, así que no hace falta revalidar el licenciamiento con `power-platform-evaluar`: sigue siendo Power Apps Premium per user para los 4 usuarios (RNF-01).

- **App model-driven única "Control de Arreglos de Pago".**
  - Cubre los dos perfiles (oficial y jefe) con roles de seguridad distintos (RNF-04).
  - Áreas de navegación: Conversaciones, Clientes y arreglos, Supervisión (jefe) y Administración del formato (jefe).
  - **Diferido a etapa 3, entregable obligatorio de su lista de control:** el diseño de pantallas. Incluye sitemap, vistas por perfil, formularios y el gesto que dispara cada transición: tomar conversación, responder con adjuntos, finalizar, unir conversaciones, reabrir o separar un correo llegado a una finalizada, generar formato para firma, desbloquear, formalizar, descartar, registrar pago, anular, reabrir un arreglo y publicar versión de plantilla.
  - También son parte de ese entregable, con el mecanismo ya decidido en Lógica:
    - gráficos del sistema (promedio de tiempo por respuesta y tiempo total, agrupados por oficial) y un dashboard de supervisión;
    - una columna de fórmula para la antigüedad;
    - las vistas de correos sin respuesta, de correos llegados a conversaciones finalizadas, de finalizadas sin respuesta, de errores de envío, de borradores y de arreglos en riesgo;
    - el filtro por producto crediticio en las vistas de arreglos (RF-17): una vista por producto más el filtro de columna nativo del grid.
  - El mockup validado (versión 3, `mockup/index.html`) es la referencia funcional de pantallas y gestos.
  - No se excluye del proyecto, solo se difiere por ser sobrediseño en esta etapa.
- **Dataverse, única fuente de datos (RNF-03).** Tablas propias de la solución, todas de propiedad de team:
  - Cliente.
  - Conversación: agrupa los correos de un hilo con un cliente (antes "Solicitud", una por correo; cambió con la validación del mockup).
  - Arreglo.
  - Cuota: hija del arreglo, con los datos de pago incluidos.
  - Versión de plantilla: con columna File y huella.
  - Mapeo de celda: hija de la versión.
  - Generación de formato: bitácora de quién generó qué y con qué versión.
  - Se usan además las tablas estándar Queue, Queue Item, Email, Attachment (ActivityMimeAttachment, adjuntos de correo) y Mailbox. Case, SLA, Routing rule set, Entitlement y Knowledge article no se usan (RNF-06).
- **Business unit hija dedicada "ARP".** Es el límite de aislamiento en el entorno compartido. Contiene los teams, los usuarios de ARP y la cuenta de servicio (ver Seguridad).
- **Server-side synchronization sobre una Queue con mailbox.**
  - El buzón compartido de Exchange Online se modela como Queue con su registro Mailbox, ambos dentro de la BU ARP.
  - Filtro de entrada "All email messages", para capturar también remitentes que no existen como clientes (RF-01).
  - Procesamiento de entrada y salida configurado en el propio registro del mailbox, no en la configuración de la organización (RNF-06).
- **Paquete de plugins .NET (C#), adoptado desde el primer día con Open XML SDK como ensamblado dependiente.** Contiene:
  - Registro de correos entrantes en conversaciones, asíncrono.
  - Registro de respuestas enviadas y cálculo de tiempos, asíncrono.
  - Reglas síncronas de Cliente, Conversación, Arreglo, Cuota, Plantilla y Mapeo, incluidos los gestos con motivo (finalizar, formalizar, desbloquear, descartar, anular, reabrir).
  - Generador del calendario de cuotas.
  - Cinco Custom API:
    - generación del formato;
    - reconciliación de correos sin conversación;
    - "Tomar" conversación, con concurrencia optimista;
    - unir conversaciones (RF-14);
    - iniciar una conversación nueva con el correo llegado a una finalizada (RF-15).
- **Web resource JavaScript mínimo.** Solo los botones del command bar que invocan esas Custom API, piden la confirmación previa y disparan la descarga del archivo. No contiene lógica de negocio. Los adjuntos y las imágenes de las respuestas usan el editor de correo nativo del timeline, sin código (RF-03).
- **Power Automate complementario (monitoreo).**
  - Un flujo disparado por cambios de estado del Mailbox y otro recurrente de salud del buzón.
  - Un tercer flujo diario solo si se activa el respaldo de D-11.
  - Son propiedad de la cuenta de servicio licenciada (RNF-06).
- **Trabajos recurrentes de eliminación masiva (BulkDelete)** para la retención de 2 años (RNF-07).
- **Cuenta de servicio licenciada.** Es propietaria de los flujos y de los trabajos de purga. Tiene dos roles separados: "ARP Servicio" (flujos y reconciliación) y "ARP Purga" (trabajos de eliminación).

### Datos
- **Cliente.**
  - Columnas: nombre, cédula, correo, celular (RF-05). Es tabla propia, no Contact.
  - La cédula no es clave alterna. Un plugin valida su unicidad al crear, porque no está confirmado que una columna con seguridad por columna pueda ser parte de una clave alterna.
  - Las conversaciones de remitentes desconocidos quedan con el cliente vacío. El oficial lo vincula o lo crea después. Si el correo del remitente coincide con un cliente existente, el plugin lo vincula automáticamente.
  - Cliente no se audita, para no copiar la cédula y el contacto a los logs de auditoría. Sus cambios solo quedan en las columnas de sistema de modificación.
- **Conversación** (RF-01, RF-14, RF-15). Reemplaza a la Solicitud de una fila por correo.
  - Agrupa los correos de un hilo con un cliente: los entrantes y las respuestas apuntan a la conversación con la columna Regarding del Email.
  - Columnas principales:
    - huella SHA-256 (64 caracteres) del Message-ID del primer correo, normalizado antes de calcularla (sin espacios ni `<>` de los extremos). Es la clave alterna y la base de la idempotencia al crear la conversación, porque un Message-ID largo podría superar el límite de tamaño de claves;
    - fecha y hora del primer correo, tomada de `actualend` del Email (el spike 3 confirmó que coincide con la recepción en Exchange en 6 de 6 casos) y no de `createdon`, para no sumar la demora de la sincronización de RNF-08;
    - cliente (opcional);
    - oficial asignado (RF-02);
    - estado: statecode Activo con motivo Abierta; Inactivo con motivo Finalizada o Unida;
    - "sin respuesta desde": la fecha del primer correo del cliente que todavía no tiene respuesta, o vacía si no falta responder nada. La mantiene el sistema; las vistas de pendientes y la antigüedad salen de ella (RF-04);
    - indicador "correo nuevo en finalizada", que el sistema marca cuando llega un correo a una conversación finalizada (RF-15);
    - resultado de la finalización (Con arreglo de pago, Sin arreglo de pago, Sin respuesta), motivo de "Sin respuesta" (spam, respuesta automática, el cliente no volvió a escribir u otro) con detalle, usuario y fecha de finalización (RF-15);
    - tiempo de la primera respuesta, tiempo total (del primer correo a la finalización) y cantidad de respuestas, en minutos y persistidos (RF-03);
    - "unida a": lookup a la conversación que recibió sus correos (RF-14);
    - asunto y remitente del primer correo, copiados por el sistema, para que las vistas muestren de qué se trata y de quién es sin abrir el timeline (diccionario de datos, P-10);
    - antigüedad, como columna de fórmula calculada al leer sobre "sin respuesta desde" (RF-04).
  - **Email (tabla estándar), columnas propias:** tiempo de esa respuesta en minutos y "respondido por" (el oficial que envió), escritas por el sistema al enviarse. Sostienen el promedio por respuesta y por oficial (RF-03, RF-04).
  - **Adjuntos e imágenes (RF-03, RNF-11).** Los adjuntos de entrada y de salida son filas de Attachment (ActivityMimeAttachment) del Email, y se borran con él. Las imágenes inline de los correos **entrantes** llegan como Attachment con su `attachmentcontentid` (spike 3). Las imágenes que el oficial pega en una respuesta quedan **en Base64 dentro del cuerpo del Email**, no en `msdyn_richtextfiles` (spike 4, probado desde la interfaz), y el cliente las recibe como imagen inline: se borran con el Email y no necesitan purga propia. Costo: cada imagen pegada agranda el cuerpo del correo; el tamaño máximo del cuerpo se verifica en la construcción.
- **Arreglo (RF-06).**
  - Columnas del formato: producto (préstamo o tarjeta), número de producto, tipo (Temporal o Cancelación), moneda, saldo inicial, monto abonado, saldo remanente, plazo (hasta 60), cuota mensual, total a pagar, fechas de inicio y fin, cuenta a debitar, oficial, fecha de formalización (valor almacenado, no calculado) y comentarios.
  - Moneda (D-36): columna requerida con un choice global de dos opciones, Córdobas y Dólares. Hay arreglos en córdobas y en dólares, pero un arreglo nunca mezcla monedas. Los montos del arreglo y de sus cuotas son decimales con dos decimales y se interpretan en la moneda del arreglo. No se usa el tipo Moneda de Dataverse. El choice de campos mapeables incluye "Moneda", para que el formato pueda imprimirla.
  - Canal de origen: columna requerida con un choice global (Correo, Telefono, Presencial, Sucursal). Un arreglo puede originarse fuera del correo, por eso la conversación de origen es opcional y el canal no.
  - Saldo remanente (= saldo inicial − monto abonado) y total a pagar (= el menor entre cuota mensual × plazo y el saldo remanente): los calcula el sistema y son columnas solo-sistema (ítem 7).
  - Lookups: cliente, conversación de origen (opcional, porque un arreglo puede originarse por teléfono, presencial, en una sucursal o por correo; ver supuestos) y versión de plantilla.
  - Número de arreglo (RF-07): vacío en borrador; lo fija el sistema al formalizar. Clave alterna sobre (producto, número de producto, número de arreglo). El spike 2 (2026-10-08) confirmó que la clave admite varios borradores con número vacío para el mismo crédito y rechaza el duplicado con número; el plan B de D-33 no hace falta.
  - Número provisional impreso: el que salió en el formato para firma; el sistema lo compara al formalizar (RF-16).
  - Estado: statecode Activo con motivo Borrador o Vigente; Inactivo con motivo Terminado, Anulado o Descartado (RF-16).
  - Hito de congelamiento: fecha de la generación del formato para firma. Desde ahí los datos del acuerdo quedan bloqueados. En un borrador, el oficial puede desbloquearlo antes de formalizar; en un vigente, solo la jefatura reabre.
  - Formalización: usuario y fecha. Descarte: motivo, usuario y fecha (RF-16).
  - Instantánea del cliente al congelar: nombre, cédula, correo y celular copiados del Cliente, exactamente los datos que imprime el formato. Los escribe el sistema al congelar y se vacían al reabrir. La cédula copiada tiene el mismo tratamiento de seguridad por columna que la del Cliente (D-19), no se audita y no entra en quick find ni en vistas. Se purga con el arreglo.
  - Anulación: motivo, usuario y fecha (RF-12).
  - Reapertura: motivo, usuario, fecha y contador de reaperturas (RF-10, RF-12).
  - Fecha de término (terminación, anulación o descarte), que alimenta la retención (RNF-07).
- **Cuota (RF-08, RF-10).**
  - Hija parental del arreglo, con Cascade All en delete. Es una fila por cuota, hasta 60.
  - Columnas: número, fecha de vencimiento, monto, fecha de pago y monto pagado. La cuota hereda la moneda de su arreglo y no lleva columna de moneda (D-36).
  - Se modela un único pago por cuota, porque el formato tiene un par fecha y monto por cuota.
- **Versión de plantilla.**
  - Columnas: número de versión (por ejemplo v07), archivo .xlsx en columna File con `MaxSizeInKB` dimensionado (unos pocos MB), huella SHA-256 del archivo, capacidad de filas del bloque de cuotas, estado (Borrador, Vigente o Histórica), aprobador y fecha de aprobación.
  - Hay una sola versión Vigente a la vez.
- **Mapeo de celda.**
  - Hija de la versión. Cada fila tiene hoja, celda en notación A1, tipo y campo de origen.
  - Tipos: valor simple, total calculado o columna de bloque de cuotas.
  - El campo de origen es un choice global. La lista está acoplada al código del generador y la mantiene el equipo de la solución, no el negocio.
  - La columna de bloque indica la celda de la primera cuota y las filas máximas (60).
- **Generación de formato.**
  - Bitácora con arreglo, versión usada, usuario y fecha. Es hija parental del arreglo y se purga con él.
  - El archivo generado nunca se guarda (RF-08).
- **Volumen (RNF-02).**
  - Son unas 4.000 conversaciones y 3.000 arreglos por año (el volumen de 80 correos por semana pasa a ser un techo de conversaciones, porque las respuestas del cliente se agrupan).
  - Con planes de hasta 5 años más 2 de retención, el peor caso estable es de unos 21.000 arreglos y hasta 1,26 millones de cuotas.
  - Son tablas estándar. No se usan tablas elásticas porque no soportan claves alternas ni cascada.
  - La capacidad de base de datos y de archivos se valida con el reporte del tenant. Con adjuntos e imágenes en ambos sentidos, la de archivos crece más que en el diseño original (RNF-11).
- **Relaciones no parentales y purga.** Se decide explícitamente:
  - Arreglo → Conversación, Arreglo → Cliente y Arreglo → Versión de plantilla usan Restrict en delete.
  - Orden de purga: arreglos (con sus cuotas y bitácora de generación), luego los Emails de las conversaciones finalizadas o unidas hace más de 24 meses sin arreglo vinculado (con sus adjuntos y Queue Items), luego esas conversaciones, luego las imágenes de esos correos en `msdyn_richtextfiles`, luego clientes sin arreglos ni conversaciones y sin modificación en 24 meses.
  - Las versiones de plantilla y sus mapeos son catálogo: se desactivan, nunca se purgan.
- **Datos sensibles.**
  - Cédula y celular están en Cliente, y cuenta a debitar en Arreglo.
  - El número de préstamo o tarjeta está en Arreglo, en claro y sin seguridad por columna. Su tratamiento queda pendiente de Cumplimiento (C-02) con un plan B de enmascarado (ver D-29).

### Lógica
Toda regla que debe cumplirse sin importar el punto de entrada vive en plugins del servidor. El cliente (formulario y botones) solo da feedback.

**Contexto de ejecución por step.** Cuando el step corre como sistema, escribe explícitamente el usuario iniciador (`InitiatingUserId`) en las columnas de trazabilidad (quién anuló, quién reabrió, quién cerró), para que la auditoría no muestre solo "SYSTEM".
- **Como sistema:**
  - Registro de correos entrantes en conversaciones y reconciliación.
  - Mantenimiento de las columnas derivadas de la conversación ("sin respuesta desde", indicador de correo en finalizada, tiempos y cantidad de respuestas).
  - Unir conversaciones e iniciar una conversación nueva (mueven correos de una conversación a otra).
  - Fijar la versión Vigente de plantilla en el arreglo.
  - Crear o reemplazar cuotas (el oficial no tiene Create ni Delete sobre Cuota).
  - Registro de la respuesta enviada y cálculo de tiempos.
  - Asignar el número de arreglo al formalizar.
  - Paso a Terminado.
  - Lectura de plantilla y mapeo en la generación.
  - Escritura de la bitácora de generación.
  - Las consultas auxiliares de los validadores (`max+1`, pertenencia a team, configuración).
- **Con la identidad del llamador:**
  - Validaciones de formulario y de gestos (finalizar, formalizar, desbloquear, descartar, anular, reabrir).
  - Lectura de arreglo, cliente y cuotas en la generación.
  - "Tomar" conversación.
- **Con la cuenta de servicio:** flujos y trabajos de purga.

**Identidad del actor bajo elevación.** Las Custom API que escriben como sistema (unir, iniciar conversación nueva, reconciliación) capturan quién llamó antes de elevar y lo pasan explícito en la escritura, porque bajo un servicio de SYSTEM los steps anidados ven SYSTEM como `UserId` e `InitiatingUserId` en toda la cadena [origen: registro de tropiezos de `power-platform-construir`, A-23].

**Columnas solo-sistema (D-30).** Los plugins pre-update de Conversación y Arreglo rechazan cualquier cambio del usuario sobre las columnas que gestiona el sistema; solo los aceptan si la identidad de ejecución es la del sistema o la cuenta de servicio.
- Conversación: "sin respuesta desde", indicador de correo en finalizada, huella del Message-ID, fecha del primer correo, asunto y remitente, tiempos, cantidad de respuestas, "unida a", usuario y fecha de finalización. El estado lo cambia el usuario solo con los gestos: Finalizar (con resultado, y motivo y detalle si es "Sin respuesta") y Reabrir. El estado Unida lo fija solo la Custom API de unir.
- Arreglo: saldo remanente y total a pagar (los calcula el plugin, ítem 7), hito de congelamiento e instantánea del cliente, número de arreglo, número provisional impreso, usuario y fecha de formalización, contador, usuario y fecha de reapertura, fecha de término, versión de plantilla, y usuario y fecha de anulación y de descarte. Los pasos a Vigente (Formalizar), Descartado, Anulado y la reapertura se aceptan solo con su gesto; los motivos son lo único que escribe el usuario.

**Propietario fijado por plugin (D-31).** Un plugin pre-create fija el team ARP como propietario de Cliente (lo crea el oficial) y de Arreglo. La Conversación la crea como sistema el plugin del ítem 1, que fija el team en el mismo create. Un plugin pre-update rechaza, en Cliente, Conversación y Arreglo, cualquier propietario distinto del team ARP. El oficial necesita Assign solo en Cliente y Arreglo (se confirma en el spike).

1. **Entrada de correo en conversaciones (RF-01).**
   - La sincronización crea el Email en la BU ARP. Si el correo responde a otro ya registrado, la correlación por el encabezado `In-Reply-To` (método por defecto de la server-side synchronization, sin falsos positivos según Microsoft) lo deja con el mismo Regarding, es decir, en la misma conversación. Confirmado en el spike 3 (`correlationmethod` = InReplyTo), también con el asunto cambiado: desde Graph (spike 3) y desde Outlook web por una persona, con un asunto totalmente distinto (spike 4). Con Outlook de escritorio no se probó.
   - Un plugin asíncrono post-create sobre Email:
     - si el Email ya llegó con Regarding a una conversación, la recalcula: si está Abierta, actualiza "sin respuesta desde"; si está Finalizada, marca el indicador de correo nuevo en finalizada y no la reabre (RF-15);
     - si no tiene Regarding, crea una conversación nueva, idempotente por la clave alterna de la huella del Message-ID, y le apunta el Email.
   - Asigna el Email al team ARP si la sincronización lo dejó con otro dueño, y trata de vincular al cliente por correo.
   - No se usa smart matching: es una configuración de toda la organización, Microsoft no la recomienda por sus falsos positivos, y el entorno es compartido (RNF-06). Un correo que el cliente manda en un hilo nuevo abre una conversación nueva; se une a mano (ítem 5).
   - Si el plugin falla, el correo igual queda guardado. El flujo de salud detecta los correos sin conversación y reinvoca la Custom API de reconciliación, que reutiliza la misma lógica.
2. **Asignación (RF-02).**
   - "Tomar" es una Custom API con la identidad del llamador. Setea el oficial asignado con concurrencia optimista (RowVersion): si otro oficial la tomó antes, el segundo recibe un mensaje claro.
   - Un plugin pre-update en Conversación protege además cualquier otra vía de edición. El oficial asignado puede pasar de vacío a uno mismo (gesto "Tomar") sin más condición. Cualquier otro cambio exige que `InitiatingUserId` pertenezca al team "ARP Jefatura", consultado por pertenencia al team. Esto cubre pasar de un oficial a otro, asignar a un tercero y dejarla sin asignar.
   - El propietario de la fila no cambia: es el team, por lo que todo el área ve la asignación.
3. **Respuesta (RF-03, RNF-11).**
   - El oficial responde con el editor de correo del timeline de la Conversación, que sale desde el buzón compartido. Adjunta archivos e inserta imágenes con el mismo editor, sin código propio.
   - **El hilo solo se conserva si la respuesta se crea con `parentactivityid` apuntando al correo que responde** (spike 3): con solo el Regarding, sale sin `In-Reply-To` y el cliente la ve como un hilo nuevo, lo que rompe la correlación de su próxima respuesta. El spike 4 confirmó, desde la interfaz, que el botón Responder (del timeline y del formulario del correo) completa `parentactivityid` con el correo respondido y que la respuesta llega con `In-Reply-To`: no hace falta plugin para eso.
   - **El remitente por defecto de una respuesta es el usuario que la escribe, no la queue** (spike 4: la respuesta salió desde el buzón personal del usuario). Si sale así, la próxima respuesta del cliente va al buzón personal del oficial y nunca entra a la solución. Un plugin pre-create sobre Email saliente con Regarding a una Conversación **fija el remitente en la queue del área** y rechaza cualquier otro; el formulario puede proponerla por defecto, pero la regla vive en el servidor.
   - El tamaño máximo por archivo es la configuración de toda la organización `MaxUploadFileSize`: 5 MB por defecto, medidos sobre el Base64 (unos 3,7 MB reales), hasta 128 MB. Los tipos bloqueados son la lista del entorno. Ambos se acuerdan con el administrador, porque afectan a todas las soluciones del entorno compartido.
   - Cuando la respuesta pasa a Sent, un plugin asíncrono escribe en el Email el tiempo de esa respuesta (`fecha de envío − "sin respuesta desde"`) y quién respondió, y recalcula la conversación: vacía "sin respuesta desde", suma la respuesta y, si es la primera, guarda el tiempo de primera respuesta. Una respuesta enviada sin nada pendiente (por ejemplo, un recordatorio) no genera tiempo.
   - El tiempo incluye la demora de la sincronización saliente, así que sobrestima levemente (criterio conservador).
   - Si el envío falla, "sin respuesta desde" no se vacía. Una vista "Respuestas con error de envío" lo muestra.
4. **Supervisión (RF-04).**
   - "Sin respuesta" es una conversación con "sin respuesta desde" no vacío, o con el indicador de correo nuevo en finalizada, en una vista ordenada por esa fecha ascendente. La antigüedad sale de la columna de fórmula.
   - Los tiempos por oficial salen de gráficos del sistema dentro de un dashboard de supervisión para el jefe: promedio por respuesta sobre Email (agrupado por "respondido por") y tiempo total sobre Conversación (agrupado por oficial asignado, sin las finalizadas "Sin respuesta", a confirmar con negocio). Las vistas no agregan promedios.
   - Una vista vacía de correos sin respuesta es la comprobación del criterio de éxito 4 del jefe, sin revisar correo por correo.
   - No se usa SLA ni KPI (RNF-06).
5. **Unir conversaciones (RF-14).**
   - Cuando el cliente tiene otra conversación abierta, el formulario lo avisa, pero la solución no une sola: un cliente puede tener a la vez un tema de tarjeta y otro de préstamo.
   - La Custom API "Unir conversaciones" (identidad del llamador para validar, sistema para escribir) mueve el Regarding de todos los correos de la conversación de origen a la de destino, que debe estar Abierta; traslada el cliente y el oficial si el destino no los tiene; repunta los arreglos vinculados; deja el origen Inactivo con motivo Unida y "unida a"; y recalcula el destino. Todo en una transacción.
6. **Finalización y correos que llegan después (RF-15).**
   - El oficial finaliza la conversación con el gesto Finalizar, con resultado obligatorio, y motivo y detalle si el resultado es "Sin respuesta". El plugin guarda usuario, fecha y tiempo total. "Con arreglo de pago" sin un arreglo formalizado vinculado hoy solo avisa (a confirmar con negocio).
   - Si llega un correo a una conversación finalizada, el ítem 1 marca el indicador y la conversación aparece entre los correos sin respuesta. El oficial decide:
     - "Reabrir": la conversación vuelve a Abierta, se limpian el resultado y los datos de finalización, y el tiempo total se recalculará en la próxima finalización;
     - "Iniciar conversación nueva": la Custom API mueve los correos llegados después de la finalización a una conversación nueva y limpia el indicador en la anterior, que sigue finalizada.
7. **Registro del arreglo como borrador (RF-06, RF-16).**
   - Un plugin pre-operation fija el propietario (team ARP) y la versión Vigente de plantilla. El arreglo nace con motivo Borrador y sin número.
   - Rechaza el registro si no hay versión Vigente, si el plazo supera 60 o supera la capacidad de filas de la versión.
   - Si el arreglo se registra desde una conversación y el canal de origen viene vacío, el plugin completa "Correo".
   - El plugin calcula el saldo remanente (= saldo inicial − monto abonado) y el total a pagar (= el menor entre cuota mensual × plazo y el saldo remanente) al crear el arreglo y al modificar el borrador. Rechaza el registro con un mensaje claro si el monto abonado supera el saldo inicial, y si con una cuota menos ya se cubre el saldo remanente (indicando con cuántas cuotas se cubre). Decisión de negocio del 2026-10-09. Son columnas solo-sistema (D-30), no columnas calculadas ni fórmulas de Dataverse: las escribe el plugin, para que queden congeladas con el resto del acuerdo. El cálculo vive en el plugin y no solo en el formulario.
8. **Calendario (RF-08).**
   - Todas las cuotas valen la cuota mensual, salvo la última cuando cuota × plazo supera el saldo remanente: esa se achica a lo que queda, saldo remanente − cuota × (plazo − 1). El formato imprime cada cuota con su monto tal como quedó.
   - Un plugin síncrono post-create crea hasta 60 cuotas en lote, dentro de la misma transacción, como sistema. El borrador ya tiene su calendario propuesto.
   - Un arreglo sin calendario no puede existir.
   - Mientras no esté congelado ni tenga pagos, un cambio de plazo, cuota o fechas hace que el plugin reemplace las cuotas.
   - La regla de fechas de vencimiento (mismo día de cada mes, tratamiento de fin de mes) se confirma con Héctor y queda centralizada en el plugin.
9. **Firma, formalización y descarte (RF-07, RF-16).**
   - **Generar formato para firma** (sobre un borrador): congela el borrador como en el ítem 10, y guarda como número provisional impreso el siguiente libre, `max+1` de los arreglos formalizados de (producto, número de producto). Los borradores y descartados no cuentan.
   - **Desbloquear** (oficial, sin jefatura, solo en Borrador): descongela, vacía la instantánea y el número provisional. El formato ya impreso deja de valer y hay que reimprimir.
   - **Formalizar** (oficial, solo en Borrador congelado): el plugin recalcula `max+1`. Si coincide con el número impreso, lo fija como número de arreglo, pasa a Vigente y registra usuario y fecha. Si no coincide, porque se formalizó otro arreglo del mismo crédito en el intervalo, rechaza con un mensaje claro y el oficial desbloquea y reimprime. La clave alterna es la última barrera ante dos formalizaciones simultáneas: la plataforma rechaza la segunda y el oficial reintenta.
   - **Descartar** (solo en Borrador): motivo obligatorio, usuario y fecha; pasa a Inactivo con motivo Descartado, completa la fecha de término y no consume número.
   - La lista de riesgo, los pagos y la anulación solo aplican a arreglos Vigentes.
10. **Congelamiento y reapertura.**
   - Se congela en la generación del formato para firma. Esa generación guarda la fecha y copia al Arreglo la instantánea del cliente (nombre, cédula, correo y celular, tal como se imprimieron). Desde ahí, un plugin rechaza cambios a los datos del acuerdo, a las cuotas (monto y fechas) y a la versión de plantilla. Los cambios posteriores en el Cliente no alteran el documento.
   - El botón avisa antes que al generar se bloquean los datos. La vista previa con una versión Borrador de plantilla es solo del jefe y no congela.
   - Después del congelamiento solo cambian las columnas de seguimiento de pago.
   - El documento firmado corresponde a los datos congelados. Una regeneración posterior reproduce los mismos datos del acuerdo, y el calendario muestra los pagos registrados hasta ese momento.
   - Para corregir un dato congelado, o para corregir un pago en un arreglo Terminado, el jefe usa "Reabrir" con motivo obligatorio. El plugin lo permite solo si `InitiatingUserId` pertenece a "ARP Jefatura", y registra motivo, usuario, fecha y contador.
   - La reapertura vuelve el arreglo a Vigente, descongela, vacía la instantánea del cliente y limpia la fecha de término. Se vuelve a congelar en la siguiente generación, que copia de nuevo los datos vigentes del Cliente; así una corrección de cédula o de contacto se refleja al recongelar.
   - Un Anulado o un Descartado no se reabren.
11. **Pagos y riesgo (RF-10, RF-11).**
   - El oficial registra a mano fecha y monto de pago en la cuota.
   - El plugin de Cuota rechaza, explícitamente, toda escritura de pago sobre cuotas de un arreglo que no esté Vigente, incluidos los borradores (la solo-lectura nativa no se hereda a las hijas). Con el arreglo Vigente, el oficial solo puede cambiar fecha y monto de pago.
   - El arreglo pasa a Terminado cuando todas sus cuotas tienen pago registrado, y el plugin completa la fecha de término.
   - La lista de riesgo es una vista de arreglos Vigentes con al menos una cuota cuya fecha de vencimiento es anterior a hoy y sin pago registrado. El criterio es solo por fecha, y un pago parcial saca la cuota de la lista.
12. **Anulación (RF-12).**
    - Se permite solo desde Vigente. Un Terminado no se anula; si hay un error, se reabre. Un borrador no se anula: se descarta.
    - Exige motivo obligatorio, validado en el plugin, que registra usuario y fecha, y completa la fecha de término.
    - Un arreglo anulado no se reactiva: se registra uno nuevo, que toma el siguiente consecutivo.
13. **Historial y búsqueda (RF-13, RF-17).**
    - Quick find sobre Cliente (cédula, nombre, correo, celular) y sobre Arreglo (número de producto). Si el spike muestra que no funciona con seguridad por columna, aplica el plan B de D-12 y D-19.
    - El formulario de Cliente tiene un subgrid con todos sus arreglos (borradores, vigentes, terminados, anulados y descartados), con estado.
    - Las vistas de arreglos se filtran por producto crediticio con una vista por producto y con el filtro de columna nativo del grid; no requiere código (RF-17).
14. **Generación del formato (RF-08).** La Custom API recibe el arreglo y:
    - Lee arreglo y cuotas con la identidad del llamador. Los datos del cliente salen de la instantánea del arreglo si está congelado; si no, del Cliente. Lee plantilla y mapeo como sistema.
    - Descarga el .xlsx de la versión del arreglo y recalcula su huella SHA-256. Si no coincide con la guardada al publicar, rechaza la generación con un error explícito.
    - Lo abre en memoria con Open XML SDK y edita las celdas en su lugar, para conservar estilos, celdas combinadas, imagen del logo y área de impresión.
    - Aplica el mapeo:
      - los valores simples reemplazan cualquier fórmula de la celda (por eso `=TODAY()` desaparece);
      - los totales los calcula el generador desde los datos;
      - el bloque de cuotas se repite hasta el plazo.
    - Limpia las filas sobrantes, incluido el número preimpreso de cuota de la plantilla, y marca el recálculo al abrir si quedan fórmulas.
    - En un borrador, imprime el número provisional en la celda del número de arreglo.
    - Devuelve el archivo en la respuesta, escribe la bitácora y no persiste el archivo. Si el arreglo no estaba congelado, escribe el congelamiento, la instantánea y, en un borrador, el número provisional, con los mismos valores que imprimió, como sistema y en la misma operación.
    - Si falla, devuelve un error sin archivo parcial y sin congelar.
15. **Administración de plantilla y mapeo (RF-09).**
    - Solo el jefe edita versiones en estado Borrador. Al publicar, un plugin valida: celdas bien formadas, sin dos campos en la misma celda, bloque de cuotas completo y capacidad coherente. Calcula y guarda la huella del archivo.
    - Al publicar, la versión pasa a Vigente y la anterior a Histórica.
    - Una versión que no es Borrador es inmutable, tanto el archivo como el mapeo. El plugin se registra sobre Update y también sobre los mensajes de subida de archivo por bloques (commit). La huella validada al generar es la segunda barrera. Un cambio exige una versión nueva, clonada de la anterior.
    - La vista previa usa la misma Custom API sobre un arreglo existente con una versión Borrador. El plugin la acepta solo si el llamador tiene permiso de escritura sobre la plantilla (comprobado con `RetrievePrincipalAccess`).
16. **Retención (RNF-07).**
    - Trabajos recurrentes de BulkDelete por filtro de fecha relativa:
      - arreglos con fecha de término anterior a 24 meses (terminados, anulados y descartados);
      - conversaciones finalizadas o unidas con fecha de finalización o unión anterior a 24 meses y sin arreglo vinculado, **borrando primero sus Emails y después la conversación**: el spike 3 mostró que borrar la conversación deja huérfano el archivo del adjunto (fila `attachment`), mientras que borrar el Email lo limpia junto con sus adjuntos y Queue Items;
      - (las imágenes pegadas en las respuestas viven dentro del cuerpo del Email y se van con él; no hace falta un trabajo propio, spike 4);
      - clientes sin modificación en 24 meses y sin arreglos ni conversaciones ("no contiene datos" sobre ambas relaciones).
    - Un borrador que nunca se formaliza ni se descarta no tiene fecha de término. Operación los revisa (ver Operación) para que no queden vivos sin plazo.
    - El plazo de un cliente corre desde su última modificación, y solo se evalúa cuando ya no tiene registros relacionados. Un cliente recién creado no se purga.
    - Siguen la cascada y el orden descritos en Datos.
    - El resultado de cada corrida se revisa en Operación.

### Integraciones
- **Exchange Online** (RF-01, RF-03, RNF-08).
  - Entrada y salida por server-side synchronization del buzón compartido modelado como Queue con mailbox (filtro "All email messages").
  - Es asíncrona: tras cada ciclo espera 5 minutos y no tiene SLA. Se acepta una demora de 5 a 15 minutos (RNF-08).
  - Solo sincroniza buzones, no grupos de distribución.
  - Un buzón se asigna a una sola instancia de Dataverse, y dentro de ella a **una sola queue**: en el entorno de desarrollo, un buzón sincronizado por dos queues generó dos Queue Items por correo y dejó indefinido el propietario del Email (spike 3). Se verifica en cada entorno.
  - Las respuestas salen siempre del buzón compartido y quedan en la conversación por la columna Regarding. Las respuestas del cliente en el mismo hilo se correlacionan por `In-Reply-To`, que está activo por defecto en la configuración de correlación de la organización; la solución no la cambia. Si el cliente cambia el asunto, algunos clientes de correo quitan ese encabezado y el correo abre una conversación nueva, que se une a mano (RF-14).
  - Los adjuntos de entrada y salida viajan con el correo, dentro del límite de tamaño y de tipos del entorno (RNF-11).
  - El acceso al buzón en Exchange se restringe a la identidad de sincronización, que recibe en el procedimiento de Exchange de cada entorno el permiso Send As sobre el buzón compartido. Los oficiales no reciben acceso Outlook: envían desde la solución con su rol (Write sobre el Email y AppendTo sobre la Queue, confirmado en el spike 4), con la Queue como remitente. El spike de sincronización valida la combinación de ambos permisos. A confirmar con mensajería.
- **Power Automate** (monitoreo).
  - Conectores Dataverse y Office 365 Outlook, ambos en el mismo grupo DLP.
  - Los flujos son solution-aware, con connection references.
  - Los datos Sensible no viajan por flujos, y los avisos incluyen solo conteos.
- **AS400 (RNF-05).** Sin integración. Saldos y pagos se cargan a mano.
- **SharePoint y OneDrive.** No se usan para nada, ni para datos, ni para la plantilla, ni para el documento generado (RNF-03).
- **Documento descargado.**
  - Sale al navegador del oficial. Es inherente a imprimir para firma y es el mismo riesgo que hoy.
  - Si Descargas está redirigida a OneDrive (Known Folder Move), el documento llegaría a OneDrive. Se consulta a TI (ver Incógnitas abiertas).

### Seguridad
- **Aislamiento en el entorno compartido (RNF-04, RNF-03, RNF-10).**
  - Se crea una business unit hija "ARP", con los teams y los usuarios de ARP en ella. Una sola BU más un team no alcanza: usuarios de otras soluciones con roles de profundidad Organización o BU padre podrían leer correos con datos Sensible.
  - Todos los registros de ARP los posee el team del sistema de esa BU: el que Dataverse crea solo con la BU, con su mismo nombre (`teamtype` propietario, `isdefault` verdadero; verificado en Dev el 2026-10-09). Su membresía la administra la plataforma: son todos los usuarios de la BU, así que "pertenecer al área" es lo mismo que "estar en la BU" y no hace falta un grupo de Entra para él. No se usa sharing manual (decisión del usuario del 2026-10-09, opción A).
  - El team del sistema es dueño de la Queue. Un segundo team de grupo "ARP Jefatura" recibe el rol del jefe.
  - **Verificación con el administrador antes de cargar datos reales, y repetida antes de cada pase a Prod.** Se revisa que ningún rol ajeno tenga profundidad Organización, o BU padre con hijas, sobre Email, Attachment (adjuntos de correo), `msdyn_richtextfiles` (imágenes del editor), Queue, Queue Item, Mailbox ni las tablas propias de ARP. Los adjuntos y las imágenes pueden traer datos Sensible: una foto de cédula, un estado de cuenta. Se guarda la evidencia del reporte.
  - Mover usuarios a una BU distinta les quita sus roles actuales. El administrador confirma el impacto sobre otras soluciones (ver Supuestos de arquitectura).
  - **Riesgo residual declarado ante Cumplimiento (C-02):** el System Administrator, y cualquier rol con profundidad Organización, siguen viendo estos datos. Se mitiga acotando la lista de administradores del entorno y con auditoría de accesos si Cumplimiento la exige.
- **Roles.** Los roles se crean en la BU ARP, con profundidad BU:
  - **"ARP Oficial":**
    - Cliente: Create, Read, Write, Append, AppendTo y Assign (solo para fijar el team como propietario). Sin Delete.
    - Conversación: Read, Write, Append y AppendTo. Las conversaciones las crea el sistema.
    - Arreglo: Create, Read, Write, Append, AppendTo y Assign. Assign, igual que en Cliente, es solo para fijar el team como propietario; los plugins pre-update rechazan cualquier propietario distinto del team ARP.
    - Cuota: Read y Write. Create y Delete los hace el sistema, y el plugin limita la escritura a las columnas de pago.
    - Email: Create, Write, Append (profundidad Basic) y Read, AppendTo (profundidad BU). **No existe un privilegio "Send"**: `SendEmail` exige Write sobre el Email. **No hace falta "Send Email as Another User"** para enviar con la Queue como remitente (spike 4, 2026-10-08, con una identidad no administradora; el correo llegó con la queue como remitente y en el mismo hilo). **Sin Delete**, porque el correo es la evidencia de RF-03. Los borradores abandonados los purga la retención.
    - Adjuntos de correo (Attachment): crear y leer, para adjuntar archivos (RF-03). Los privilegios exactos se confirman en la construcción. No se otorgan permisos sobre `msdyn_richtextfiles`: el editor de correo no la usa (spike 4).
    - Queue: Read y **AppendTo** (lo exige la plataforma para poner la queue como remitente; spike 4). Queue Item no tiene privilegios propios de lectura que asignar.
    - Versión de plantilla: Read y AppendTo, para fijar el lookup del arreglo. Sin acceso a Mapeo.
    - Generación de formato: Read.
    - Sin acceso al Mailbox.
  - **"ARP Jefe":** suma Create, Write y Append sobre Versión de plantilla y Mapeo, Delete solo sobre Mapeo en Borrador (el plugin bloquea el resto), y las vistas y el dashboard de supervisión. La reasignación y la reapertura las comprueba el plugin por pertenencia a "ARP Jefatura".
  - **"ARP Servicio"** (cuenta de servicio, flujos y reconciliación): lectura sobre Mailbox, Email, Queue Item, Conversación y Arreglo; Create sobre Conversación, que es el privilegio de ejecución de la Custom API de reconciliación; escritura sobre Cuota solo si se activa el respaldo de D-11.
  - **"ARP Purga"** (cuenta de servicio): Read, Delete y BulkDelete sobre Arreglo, Cuota, Generación, Conversación, Email con sus adjuntos y Queue Item y Cliente. Ningún rol humano tiene Delete sobre el agregado.
- **Seguridad por columna (RNF-03, RNF-10).**
  - Perfil de columna para la cuenta a debitar, con acceso para el team ARP y la jefatura.
  - La cédula se protege igual, **confirmado en el spike 4**: un usuario sin el perfil de columna no encuentra la fila buscando por cédula con quick find (0 filas, sin error) ni puede deducirla con filtros `eq` o `ne`; con el perfil la encuentra y ve el valor. Los oficiales y la jefatura reciben el perfil por su team, así que la búsqueda del criterio de éxito 6 funciona para el área. Falta comprobar que la pertenencia al perfil por team (y no por usuario) se comporte igual. La cédula copiada en la instantánea del arreglo recibe siempre el mismo tratamiento que la del Cliente. No se extiende a más columnas.
- **Contextos de ejecución y privilegio de ejecución de las Custom API.** Los contextos de Lógica cumplen el privilegio mínimo: el sistema solo hace lo que el oficial no puede, y la lectura de datos de negocio en la generación va con la identidad del llamador. Cada Custom API se restringe con su privilegio de ejecución (Execute Privilege Name), el de una tabla propia de ARP presente solo en los roles que corresponden: generación de formato con Read de Generación de formato (Oficial y Jefe); "Tomar", unir conversaciones e iniciar conversación nueva con Write de Conversación (Oficial y Jefe); reconciliación con Create de Conversación (solo "ARP Servicio").
- **Flujos (RNF-03).**
  - Propiedad de la cuenta de servicio licenciada (RNF-06), dedicada a ARP y ubicada en su BU.
  - Secure inputs y secure outputs activados en todas las acciones sobre tablas de ARP, para que el historial de ejecuciones no muestre cuerpos de correo.
  - Los flujos no leen cuerpos ni adjuntos.
- **Auditoría de Dataverse (RNF-10, RNF-03).**
  - Se audita: Arreglo (estado, fechas de control, formalización, descarte, anulación, reapertura, versión de plantilla), Cuota (pagos), Conversación (asignación, finalización con resultado y motivo, reapertura, unión), Versión de plantilla, Mapeo y Generación. Registra quién cambió el mapeo y cuándo.
  - No se auditan Cliente, la cédula (ni la copiada en la instantánea del arreglo), la cuenta a debitar ni el número de producto, para que los logs no repliquen datos de identidad.
  - La retención de los logs de auditoría es del entorno compartido y no se purga con los trabajos de la solución. Es un pendiente de C-02 que bloquea el cierre de RNF-10 (ver Incógnitas abiertas). La propuesta es una ventana de retención de logs no mayor a la de datos, definida con el administrador.
- **Datos en entornos no productivos.** Dev y Test usan datos de prueba. No se copian datos reales de Prod.
- **Trazas.** Los plugins no escriben datos Sensible en trazas.

### Entornos y despliegue
- **Entornos.** Dev, Test y Prod de Nicaragua, compartidos y administrados (RNF-06). Se coordinan con el administrador los recursos únicos por entorno: registro de ensamblados, pool de requests, configuración de pipelines y, si se usa, Application Insights.
- **Solution.**
  - Una única solution con un publisher dedicado, unmanaged solo en Dev. Test y Prod reciben managed.
  - Environment variables y connection references en lugar de valores fijos. Las variables son: nombres de los dos teams, destinatarios de alertas y umbrales de monitoreo.
  - El mecanismo de despliegue son los Power Platform pipelines, dado que los entornos son administrados. Si el host no existe, es un punto a coordinar con el administrador (ver Supuestos de arquitectura).
- **Teams por nombre, por entorno.**
  - El GUID de un team y su grupo de Entra cambian en cada entorno. Los plugins resuelven la BU ARP por el nombre que indica una environment variable; el team propietario es el team de esa BU con `isdefault` verdadero (no depende de un nombre) y el team Jefatura se busca por el nombre de otra environment variable, dentro de la BU.
  - Se documenta el grupo de Entra de cada entorno en el procedimiento de configuración.
- **Buzones por entorno.**
  - Un buzón corresponde a una instancia de Dataverse. Dev y Test usan buzones compartidos de prueba propios. Solo Prod usa el buzón real del área.
  - La configuración de sincronización se hace en el registro del mailbox de la Queue, nunca en la configuración de la organización.
- **Configuración de la organización que la solución usa pero no cambia.** La correlación de correos por encabezados (activa por defecto), el tamaño máximo de adjuntos y la lista de extensiones bloqueadas (RNF-11) son de toda la organización. Se verifican en cada entorno y cualquier cambio lo decide el administrador, porque afecta a las demás soluciones.
- **Datos de configuración.** La BU, los teams, la Queue, el Mailbox y las versiones de plantilla con su mapeo son datos, no componentes de solución. No viajan con ella. Se documentan en un procedimiento por entorno, y las plantillas se promueven de Test a Prod con la herramienta de migración de configuración o una recarga controlada por el jefe (se decide en etapa 3).
- **Trabajos de eliminación masiva.** Tampoco son componentes de solución. Se crean por entorno en Prod con la identidad de la cuenta de servicio, y en Test para validarlos.
- **Orden de construcción por riesgo (RNF-09, C-01, C-03).** Los spikes se adelantan a lo demás; el plazo y la fase única se resuelven en las secciones 7 y 8. Plan B de cada spike:
  - Correlación de respuestas en la misma conversación: si la correlación por `In-Reply-To` no deja el Regarding del correo anterior, el plugin del ítem 1 busca por `In-Reply-To` el Email ya registrado y toma su conversación.
  - Clave alterna del arreglo con número vacío en borradores: confirmada en el spike 2; sin plan B.
  - Imágenes pegadas en las respuestas: resuelto en el spike 4 (quedan dentro del cuerpo del Email).
  - Sincronización del buzón (permisos, propietario, Send As, fecha de llegada, cascada de borrado): si el dueño de la Queue no sirve, la cuenta de servicio es la dueña y un plugin reasigna los correos al team. Si la sincronización no es viable, volver a la alternativa C1 de factibilidad (flujo sobre el buzón compartido), lo que exige revisar el diseño.
  - Open XML en el sandbox (C-01): generación en JavaScript en el cliente (D-13).
  - Quick find sobre la cédula con seguridad por columna: plan B de D-19.
  - Columna de fórmula con Now() para la antigüedad: confirmada en el spike 2, con condiciones (ver D-27).
  - Operador de fecha relativa en la vista de riesgo: confirmado en el spike 2 (ver D-11); el flujo diario de respaldo no hace falta.
  - Inmutabilidad del archivo: si la subida por bloques no dispara el step, la barrera es la huella validada en cada generación (D-15).
- **ALM.** Los plugins se compilan en un paquete NuGet incluido en la solución.

### Operación
- **Responsables.**
  - El equipo de automatización mantiene la solución.
  - El administrador de Power Platform y Exchange atiende el buzón, la BU y los entornos.
  - El jefe del área atiende lo funcional: la plantilla, los correos sin respuesta, los correos llegados a conversaciones finalizadas, las reaperturas y los reportes.
- **Monitoreo de la captura de correos (RNF-08, cumple el criterio de éxito 1).**
  - El flujo de estado del mailbox avisa por Outlook al jefe y al equipo cuando la entrada o la salida del buzón pasan a fallo.
  - El flujo recurrente cada 30 minutos en horario hábil comprueba tres cosas: que haya correos entrantes recientes (la ausencia no puede ser un evento, por eso se justifica un recurrente y no un trigger), que no haya correos sin conversación y que no haya respuestas con error de envío.
  - Si hay correos sin conversación, invoca la Custom API de reconciliación.
- **Borradores sin movimiento.** Una vista para el jefe lista los borradores sin cambios en más de 30 días, para formalizarlos o descartarlos. Sin esto, un borrador olvidado no tiene fecha de término y la retención nunca lo alcanza (RF-16, RNF-07).
- **Plugins asíncronos.** Se supervisan en System Jobs. Se registran trazas solo de excepciones.
- **Retención.**
  - Cada corrida de purga se revisa en System Jobs. Si falla, el flujo avisa. Los fallos por Restrict se esperan reducidos, porque los filtros excluyen los registros todavía relacionados.
  - Un backup manual precede a cambios grandes. El backup automático puede conservar datos purgados hasta que expire su retención. Esa ventana, y la de los logs de auditoría, se consultan a Cumplimiento (RNF-10).
- **Permisos.** Se repite la revisión de roles ajenos con el administrador antes de cada pase a Prod, y se revisa periódicamente la lista de System Administrators (ver Seguridad).
- **Cambios de plantilla.** Los hace el jefe como versión nueva en Borrador, con vista previa y publicación. El equipo de automatización no interviene, salvo para agregar campos mapeables.
- **Capacidad.** Se vigila la capacidad de base de datos y de archivos de Dataverse en el reporte del tenant. Los adjuntos y las imágenes de los correos son el principal consumo de archivos (RNF-11).

### Decisiones
| ID | Decisión | Alternativas descartadas | Motivo | Requisitos |
|---|---|---|---|---|
| D-01 | Una sola app model-driven sobre Dataverse, con dos perfiles por rol de seguridad | Canvas app; code app (C3); model-driven con custom pages; una app por rol | Los 4 usuarios comparten datos y flujo de trabajo, y necesitan vistas de pendientes, historial y búsquedas sobre un modelo relacional, que model-driven da sin construirlo a mano. Canvas obligaría a resolver delegación sobre cientos de miles de cuotas. La code app depende de capacidades en preview y no tiene Git nativo. Los 4 ya tienen Premium, y model-driven lo exige, por lo que no hay costo nuevo | RF-02, RF-04, RF-11, RF-13, RNF-01, RNF-06 |
| D-02 | Captura por server-side synchronization de una Queue con mailbox sobre el buzón compartido, filtro "All email messages", configurada en el registro del mailbox. Confianza baja | Trigger de buzón compartido de Office 365 Outlook con flujo; Email Router; sincronizar buzones individuales | El filtro "All email messages" captura también remitentes que no existen como clientes (RF-01). El correo queda como actividad Email con su hilo, lo que sirve a RF-03 sin un flujo intermedio. La latencia de 5 a 15 minutos entra en lo aceptado (RNF-08). Configurar a nivel de buzón evita tocar la organización en entornos compartidos (RNF-06) | RF-01, RF-03, RNF-06, RNF-08 |
| D-03 | Conversación como tabla propia que agrupa los correos de un hilo (antes: una Solicitud por correo, cambiado tras la validación del mockup del 2026-10-08). Un plugin asíncrono post-create sobre Email agrega el correo a su conversación si llegó correlacionado, o crea una conversación nueva idempotente por la clave alterna de la huella SHA-256 del Message-ID del primer correo. Reconciliación por Custom API | Una solicitud por correo (diseño original); usar Queue Item o Email directamente; Case; flujo disparado por el alta del correo; clave alterna sobre el Message-ID completo | Negocio trabaja por conversación de ida y vuelta y la cierra con un resultado; una fila por correo partía cada caso en varias y no permitía finalizar el caso entero. Email y Queue Item no tienen estado de negocio, asignación, finalización ni tiempos. Case está restringida (RNF-06). Un plugin evita que los cuerpos de correo pasen por el historial de un flujo (RNF-03). Es asíncrono para que un fallo no pierda el correo, y la reconciliación cierra el 100 %. La huella evita que un Message-ID largo supere el límite de tamaño de la clave | RF-01, RF-03, RF-04, RF-14, RF-15, RNF-03, RNF-06 |
| D-04 | Propietario de todos los registros: team ARP. La asignación al oficial es una columna aparte. "Tomar" es una Custom API con concurrencia optimista. Un plugin pre-update permite solo vacío → uno mismo; todo otro cambio exige que `InitiatingUserId` pertenezca al team "ARP Jefatura" | Asignar el propietario al oficial; Routing rule set; compartir registros uno por uno; perfil de columna sobre el oficial asignado | Con propietario individual, los demás oficiales no verían la conversación, y RF-02 exige asignación visible para todos. El privilegio nativo Assign no aplica porque el propietario es el team. Un perfil de columna bloquearía también "Tomar". El plugin comprueba el rol del llamador, y la concurrencia optimista evita que dos oficiales tomen la misma conversación. Routing rule set está restringido (RNF-06) Spike 2: un Update con ETag viejo devolvió 412 y la fila no cambió; con dos "Tomar" sobre el mismo ETag gana el primero. | RF-02, RNF-04, RNF-06 |
| D-05 | Responder desde el timeline de la Conversación, con el buzón como remitente y con adjuntos e imágenes del editor nativo. Un plugin registra cada respuesta enviada: escribe en el Email el tiempo de esa respuesta y quién respondió, y recalcula en la conversación "sin respuesta desde", la primera respuesta y la cantidad de respuestas | SLA o KPI; flujo sobre Enviados; calcular el tiempo solo en la vista; medir solo la primera respuesta | Responder desde la solución deja registro de qué y cuándo (RF-03). Con ida y vuelta, medir solo la primera respuesta ocultaría las demoras de las siguientes; el tiempo por respuesta en el Email permite promediar por oficial (RF-04). Las tablas SLA y KPI están restringidas (RNF-06). Un flujo sobre Enviados añade un punto de falla silencioso Spike 3: la respuesta conserva el hilo solo si se crea con `parentactivityid`; sale con la queue como remitente sin "en nombre de" (probado con el service principal; falta el rol del oficial). | RF-03, RF-04, RNF-06, RNF-11 |
| D-06 | Finalización manual de la conversación con resultado (Con arreglo de pago, Sin arreglo de pago, Sin respuesta), con motivo y detalle obligatorios para "Sin respuesta" (spam, respuesta automática, el cliente no volvió a escribir, otro), usuario y fecha; tiempo total persistido; vista de finalizadas sin respuesta para el jefe | Borrar correos no relevantes; dejarlos pendientes; finalizar automáticamente al responder o al formalizar un arreglo | Todo correo debe quedar contabilizado (RF-01) y los automáticos y el spam no deben inflar los pendientes. Responder no termina el caso, y el resultado lo conoce el oficial: negocio pidió que la finalización sea manual. El jefe revisa los "Sin respuesta" en su vista (RF-04) | RF-01, RF-04, RF-15 |
| D-07 | Cliente en tabla propia, vinculación por correo, cédula validada por plugin y no como clave alterna | Tabla estándar Contact; clave alterna sobre la cédula | RF-05 pide tabla propia y sin Contact. La clave alterna sobre una columna con seguridad por columna no está confirmada, así que la unicidad se garantiza en el servidor Spike 2: confirmado que una columna con seguridad por columna no puede formar parte de una clave alterna. | RF-05, RNF-03 |
| D-08 | Arreglo con Cuota como hija parental (Cascade All), el pago como columnas de la cuota (un pago por cuota) y el calendario generado por un plugin síncrono en lote, como sistema. Se reemplaza mientras no haya congelamiento ni pagos. El plazo se valida contra la capacidad de la versión | Tabla de pagos separada; calcular el calendario solo en el documento; tabla elástica | RF-06 y RF-08 exigen un calendario consultable. El formato tiene un único par fecha y monto por cuota, así que una tabla de pagos añade agregación sin beneficio. Los volúmenes de RNF-02 caben en tablas estándar, y la elástica no admite claves alternas ni cascada. El oficial no tiene Create ni Delete sobre Cuota, por eso el reemplazo corre como sistema | RF-06, RF-08, RF-10, RNF-02 |
| D-09 | Consecutivo por crédito calculado al formalizar (`max+1` de los arreglos formalizados), comparado con el número provisional impreso en el formato para firma; si no coincide, se rechaza y se reimprime. La clave alterna (producto, número, arreglo) es la garantía final ante dos formalizaciones simultáneas | Autonumérico; columna calculada; número asignado al crear (diseño original); reservar el número al imprimir; imprimir sin número | El consecutivo debe ser por crédito, no global. Asignarlo al crear haría que cada borrador descartado consuma un número e infle el conteo de RF-07. Reservarlo al imprimir tiene el mismo problema. Imprimir sin número deja incompleto el documento firmado. El número provisional cubre los tres; el único costo es reimprimir si otro arreglo del mismo crédito se formalizó en el intervalo, un caso muy poco probable | RF-07, RF-16 |
| D-10 | Estados del arreglo como statecode y motivo: Borrador y Vigente (activos); Terminado, Anulado y Descartado (inactivos). Descartar solo desde Borrador y anular solo desde Vigente, ambos con motivo, usuario y fecha. Un Terminado no se anula; se reabre. Un Anulado o un Descartado no se reactivan | Arreglo vigente desde su creación (diseño original); borrar el borrador que no se concreta; eliminar o desactivar con un indicador propio; permitir anular un Terminado | Negocio necesita preparar propuestas que pueden no concretarse sin que cuenten como arreglos ni consuman número (RF-16). Ningún rol humano tiene Delete, por eso un borrador se descarta y lo borra la retención. Un registro inactivo queda de solo lectura de forma nativa, y el bloqueo de las cuotas lo agrega el plugin de Cuota. El historial muestra todos los estados (RF-13). La fecha de término alimenta la retención (RNF-07) | RF-12, RF-13, RF-10, RF-16, RNF-07 |
| D-11 | Lista de riesgo como vista con filtro relacionado sobre cuotas vencidas sin pago. Respaldo: un flujo recurrente diario que invoca una Custom API para marcar las cuotas vencidas sin pago, con rol "ARP Servicio" | Rollup column; plugin con temporizador | Es el criterio de RF-11, solo por fecha, sin código. El rollup se recalcula con retraso. Un plugin no se dispara por reloj, así que el respaldo necesita un flujo. Solo se activa si el operador de fecha relativa no sirve con columnas de solo fecha Spike 2: ningún operador da por sí solo "fecha anterior a hoy" sobre una columna de solo fecha; la condición es `olderthan-x-days 1` OR `yesterday`, que funcionó en filtro relacionado y en vista guardada. El respaldo con flujo diario no se activa. | RF-11, RF-10 |
| D-12 | Búsqueda con quick find sobre Cliente y Arreglo, más el subgrid de arreglos en el formulario del cliente. Se valida en el spike inicial, con el plan B de D-19 si falla | Búsqueda global; canvas | Cubre la búsqueda por cédula y por número de préstamo o tarjeta con historial completo y sin depender de la configuración de búsqueda de la organización. La compatibilidad de quick find con seguridad por columna no está confirmada, y es el criterio de éxito 6 Spike 4 (2026-10-08): quick find sobre la cédula con seguridad por columna funciona para quien tiene el perfil y no expone la fila a quien no lo tiene; el plan B no se activa. | RF-13 |
| D-13 | Generador como Custom API con Open XML SDK como ensamblado dependiente (paquete adoptado desde el día uno), que devuelve el archivo sin guardarlo. Respaldo: generación en JavaScript en el cliente. Confianza alta (validado en los spikes 1 y 1b) | JavaScript en el cliente como principal; Excel Online o conversión de Power Automate; plantilla nativa de Dataverse; Azure Function; reconstruir el documento con una librería | Editar la plantilla real en memoria conserva estilos, imagen, celdas combinadas y área de impresión del documento controlado. Excel Online, OneDrive y SharePoint están prohibidos (RNF-03). Una Azure Function sacaría datos Sensible de Dataverse. El equipo domina C# (supuesto). Que el archivo no se guarda es exigencia de RF-08 Spikes 1 y 1b (2026-10-08): confirmado en local y dentro del sandbox, con salida idéntica; la confianza pasa de media a alta y el respaldo en JavaScript queda solo como contingencia. | RF-08, RNF-03, RNF-06 |
| D-14 | Plantilla en una tabla de versiones con columna File y mapeo en una tabla hija, con choice global para los campos mapeables | Plantilla en SharePoint; mapeo en un archivo de configuración; lista de campos como tabla | RF-09 exige administrar la plantilla sin tocar código y RNF-03 prohíbe SharePoint. Los campos mapeables están acoplados al generador, así que un choice global mantenido por el equipo es lo adecuado | RF-09, RNF-03 |
| D-15 | Versionado: cada arreglo referencia la versión Vigente al crearse. Una versión no Borrador es inmutable. Se protege con un plugin sobre Update y sobre la subida de archivo por bloques, y con la huella SHA-256 guardada al publicar y validada en cada generación | Guardar el documento generado; editar la plantilla vigente sin versiones; confiar solo en el step de Update | Como el documento no se guarda, regenerarlo con la versión que usó el arreglo es lo que lo hace reproducible. No está confirmado que la subida por bloques dispare el step de Update, y sin la huella un jefe podría alterar la plantilla vigente sin dejar rastro. La validación al publicar y la vista previa protegen contra un mapeo erróneo en un documento firmado | RF-08, RF-09, RNF-10 |
| D-16 | Reglas del mapeo: valores simples sustituyen fórmulas (fecha de formalización almacenada, sin `TODAY()`), totales calculados por el generador, bloque de cuotas hasta 60 con limpieza de filas sobrantes y de números preimpresos | Conservar `=TODAY()`; conservar las fórmulas de totales sin recálculo | La fecha de un documento firmado no puede cambiar al reabrirlo. La plantilla actual trae números de cuota preimpresos que aparecerían en arreglos cortos, y sus totales cubren solo 36 de 48 filas, así que v07 con 60 filas la aprueba Héctor (C-01) | RF-06, RF-08, RF-09 |
| D-17 | Botones del command bar con un web resource JavaScript mínimo que invoca las Custom API de generación, "Tomar", unir conversaciones e iniciar conversación nueva. Cada Custom API se restringe con su privilegio de ejecución, el de una tabla propia de ARP presente solo en los roles que corresponden | Command bar con Power Fx; PCF; descarga desde un flujo; Custom API sin privilegio de ejecución | Hay que invocar una Custom API y entregar un archivo, y el command bar no ofrece otra vía sin JavaScript. Es el único script y no contiene lógica de negocio. El privilegio de ejecución evita que un usuario ajeno a ARP las invoque (Read de Generación para el formato, Write de Conversación para "Tomar", unir e iniciar conversación nueva, Create de Conversación para la reconciliación) | RF-08, RF-02, RF-14, RF-15, RNF-04 |
| D-18 | BU hija dedicada "ARP" con los teams, los usuarios y la cuenta de servicio en ella. Propietario: el team del sistema de la BU (membresía administrada por la plataforma; opción A del 2026-10-09), roles Oficial, Jefe, Servicio y Purga con profundidad BU, sin Delete humano, y verificación con el administrador de que ningún rol ajeno tenga profundidad Organización o BU padre sobre Email, Queue, Queue Item, Mailbox ni las tablas propias | Una sola BU con team propietario; rol de organización; compartir registros manualmente; una BU por área funcional | En un entorno compartido por todo Nicaragua, un team propietario en la BU única no impide que usuarios de otras soluciones con profundidad BU u Organización lean correos con datos Sensible. La BU hija es el límite organizacional duro, y la verificación con el administrador cubre los roles ajenos. El riesgo del System Administrator se declara ante Cumplimiento (C-02). Sin Delete humano, el borrado solo ocurre por retención | RNF-04, RNF-03, RNF-10, RF-09, RNF-06 |
| D-19 | Seguridad por columna para la cuenta a debitar y para la cédula (confirmado en el spike 4 que quick find funciona con el perfil), y la cédula copiada en la instantánea del arreglo recibe el mismo tratamiento. Plan B descartado: cédula sin seguridad por columna, protegida por el aislamiento por BU, la auditoría de accesos que defina Cumplimiento y el control de roles | Sin seguridad por columna; extenderla a más columnas; columna de búsqueda normalizada sin seguridad | La cuenta es de los datos más críticos y no se busca. La cédula sí se busca (RF-13). Una columna normalizada sin seguridad expondría el mismo dato que se quiere proteger. La copia en la instantánea no puede quedar menos protegida que el original Spike 4: confirmado; la cédula lleva seguridad por columna y el plan B queda descartado. | RNF-03, RNF-10, RF-13 |
| D-20 | Auditoría de Dataverse solo sobre columnas de control y de pago, sin Cliente, cédula (incluida la de la instantánea), cuenta ni número de producto. La retención de los logs es del entorno y queda como pendiente bloqueante de C-02 | Auditar todo; no auditar | Cubre quién anuló, quién reabrió, quién cambió el mapeo y los cambios de pagos. Evita replicar identidad en los logs, también la copiada. Los logs no se purgan con el BulkDelete y sobrevivirían a los 24 meses, por eso se pide a Cumplimiento y al administrador una ventana de retención acorde | RNF-10, RNF-07, RF-12, RF-09 |
| D-21 | Power Automate para monitoreo (estado del mailbox, salud del buzón) y, solo si se activa el respaldo de D-11, el marcado diario de cuotas vencidas. Flujos propiedad de la cuenta de servicio licenciada, con secure inputs y outputs y avisos sin datos Sensible | Flujos propiedad de un service principal; flujo como mecanismo principal de captura; encadenar flujos | La cuenta de servicio es el estándar regional (RNF-06). Mantener los flujos fuera de la captura evita que los cuerpos de correo pasen por el historial. La ausencia de correos solo se detecta con un recurrente | RNF-06, RNF-03, RNF-08, RF-01 |
| D-22 | Retención de 2 años con trabajos recurrentes de BulkDelete: arreglos por fecha de término (terminados, anulados y descartados), los Emails de conversaciones finalizadas o unidas sin arreglo vinculado y después esas conversaciones, y clientes sin modificación en 24 meses y sin registros relacionados. Orden y cascada explícitos. Vista de borradores sin movimiento para que ninguno quede sin plazo | Retención a largo plazo nativa; archivado en Azure con reactivación; TTL en tablas elásticas; Synapse Link; purgar clientes sin filtro de edad; confiar en que borrar el Email borra sus imágenes | No hay requisito de histórico ni reactivación, solo expirar a los 2 años. La retención nativa conserva en vez de borrar. Las imágenes pegadas viven dentro del Email y se van con él (spike 4). Sin el filtro de edad, el trabajo podría borrar un cliente recién creado antes de su arreglo. Un borrador nunca resuelto no tiene fecha de término Spike 3: los Emails se purgan antes que su conversación, porque borrar la conversación deja huérfano el archivo del adjunto. | RNF-07, RNF-10, RNF-02, RF-05, RF-16 |
| D-23 | Una solution managed fuera de Dev con publisher dedicado, pipelines, buzones de prueba por entorno, datos de configuración fuera de la solution, y teams resueltos por nombre mediante environment variables | Export e import manual; compartir un buzón entre entornos; configurar la organización; GUID de team fijo | Un buzón no puede servir a dos instancias. Los entornos son compartidos y administrados (RNF-06). El GUID del team y su grupo de Entra cambian por entorno, así que se resuelven por nombre. Los spikes de plantilla y sincronización se adelantan por el plazo (RNF-09) | RNF-06, RNF-09, RNF-04, RF-01 |
| D-24 | Sin integración con el AS400: saldos y pagos se cargan a mano | Integración con RPA | RNF-05 la excluye. RF-10 define el pago como registro manual, y los saldos se consultan a mano en el AS400 | RNF-05, RF-10 |
| D-25 | Congelamiento al generar el formato para firma, con instantánea del cliente en el arreglo (nombre, cédula, correo, celular). En un borrador, el oficial puede desbloquearlo para corregir antes de la firma; al formalizar queda congelado y los cambios posteriores solo van por "Reabrir" del jefe, con motivo, usuario y fecha, que vacía la instantánea y se refresca al recongelar. El mismo mecanismo corrige pagos en un Terminado | Guardar el documento firmado; dejar editables los campos hasta el primer pago; leer siempre el Cliente vivo; exigir jefatura para corregir un borrador sin firmar | Como el documento no se guarda, solo congelar la versión de plantilla no lo hace reproducible, y la instantánea fija los datos del cliente impresos. El gesto Formalizar, descartado en el diseño original para no sumar pasos, lo pidió negocio (RF-16): separa lo impreso de lo firmado. Un borrador sin firmar no es un documento firmado, por eso el oficial lo desbloquea sin jefatura. La reapertura con motivo evita que un pago erróneo deje un Terminado irreversible | RF-06, RF-08, RF-10, RF-12, RF-16, RNF-03, RNF-10 |
| D-26 | Contexto de ejecución explícito por step: sistema para registrar correos en conversaciones, mover correos (unir, iniciar conversación nueva), fijar versión, reemplazar cuotas, registrar respuestas, asignar el número al formalizar, pasar a Terminado y escribir la bitácora; el llamador para validaciones, gestos, lectura de datos y "Tomar". Las Custom API que elevan capturan al actor antes de elevar | Ejecutar todo con el llamador; ejecutar todo como sistema | El oficial no tiene Create ni Delete sobre Cuota, ni Delete sobre Email. Todo como sistema borraría la trazabilidad de quién actuó: bajo SYSTEM, los steps anidados ven SYSTEM como iniciador. Todo con el llamador obligaría a otorgar privilegios que el rol no debe tener | RNF-04, RF-03, RF-06, RF-08, RF-14, RF-15 |
| D-27 | Supervisión con gráficos del sistema dentro de un dashboard (promedio por respuesta sobre Email, agrupado por quién respondió; tiempo total sobre Conversación, agrupado por oficial) y una columna de fórmula para la antigüedad sobre "sin respuesta desde" | Vistas con promedios; calcular la antigüedad en un flujo | Las vistas no agregan promedios por oficial. La columna de fórmula calcula la antigüedad al leer, sin proceso periódico. Se valida en el spike que admita Now() Spike 2: `Now()` solo con columnas de fecha y hora en zona local (`UTCNow()` con las independientes de zona); la antigüedad se calcula en minutos y se divide por 60, porque `DateDiff` en horas cuenta cambios de hora de reloj; la columna se crea desde el editor de Power Apps, porque definir fórmulas por código no está soportado. | RF-04, RF-03 |
| D-28 | Respuestas solo desde la solución, enviadas con la Queue como remitente. El rol del oficial incluye Write sobre el Email y AppendTo sobre la Queue (spike 4). La identidad de sincronización recibe Send As en Exchange sobre el buzón. Los oficiales no reciben acceso Outlook. Se valida en el spike de sincronización Un plugin pre-create fija la queue como remitente de toda respuesta con Regarding a una Conversación | Dar acceso Outlook a los oficiales; enviar con el correo personal del oficial | Una respuesta enviada desde Outlook no pasa por la solución y no se mediría. Sin AppendTo sobre la Queue, el oficial no puede ponerla como remitente (spike 4). Sin Send As de la identidad de sincronización, la salida no sale por el buzón. Con ambos, el 100 % de respuestas queda registrado en la conversación Spike 4: el envío con la Queue como remitente funciona con un rol no administrador sin el privilegio de enviar en nombre de otro; hace falta AppendTo sobre Queue. Spike 4: desde la interfaz, el remitente por defecto fue el usuario y no la queue; un plugin pre-create fija la queue como remitente en toda respuesta de una Conversación. | RF-03, RNF-04 |
| D-29 | Número de préstamo o tarjeta en claro y sin seguridad por columna, para la clave alterna y la búsqueda. Pendiente de Cumplimiento (C-02). Plan B: para tarjetas, guardar solo el número enmascarado y una huella SHA-256 como clave alterna | Cifrar la columna; seguridad por columna | RF-07 y RF-13 requieren el número para el consecutivo y la búsqueda. Un número de tarjeta completo puede tener requisitos más estrictos, que decide Cumplimiento. El enmascarado cambia lo que imprime el formato, y lo aprueba Héctor | RF-07, RF-13, RNF-03, RNF-10 |
| D-30 | Columnas solo-sistema en Conversación y Arreglo: los plugins pre-update rechazan cambios del usuario sobre "sin respuesta desde", indicador de correo en finalizada, tiempos, huella del Message-ID, fecha del primer correo, asunto y remitente, "unida a", datos de finalización, saldo remanente y total a pagar del arreglo (los calcula el plugin), hito de congelamiento, instantánea, número de arreglo, número provisional, datos de formalización, reapertura, descarte y anulación, fecha de término y versión de plantilla. Los cambios de estado se aceptan solo con su gesto y motivo | Confiar en solo lectura del formulario; perfiles de columna sobre cada una | Un formulario de solo lectura se sortea por la API. Los tiempos y el número de arreglo son la evidencia de RF-03 y RF-07, y el hito de congelamiento sostiene la reproducibilidad. Una columna con perfil de columna bloquearía también al sistema y a los gestos legítimos. El plugin comprueba la identidad de ejecución | RF-03, RF-07, RF-08, RF-12, RF-15, RF-16, RNF-04, RNF-10 |
| D-31 | Propietario team ARP (el team del sistema de la BU, resuelto por `isdefault`) fijado por plugin pre-create en Cliente y Arreglo (con Assign en el rol del oficial) y por el plugin asíncrono como sistema en Conversación. Un plugin pre-update rechaza cualquier otro propietario | Dejar el propietario por defecto del creador; Assign en todos los roles; reasignar después | Un Cliente creado por el oficial quedaría propiedad suya y los demás no lo verían (RNF-04, RF-05). La Conversación la crea el sistema, así que no necesita Assign. Bloquear el cambio de propietario evita que un oficial saque un registro del team Spike 3: la relación Regarding propaga Assign, Share y Delete de la conversación a sus Emails; el propietario team de la conversación se extiende a sus correos. | RNF-04, RF-05, RF-02, RF-06 |
| D-32 | Agrupación de correos: automática solo por la correlación `In-Reply-To` de la server-side synchronization (activa por defecto, sin falsos positivos); manual para correos en hilos nuevos, con la Custom API "Unir conversaciones" y un aviso cuando el cliente tiene otra conversación abierta. Un correo que llega a una finalizada no la reabre: marca un indicador, cuenta como sin respuesta y el oficial decide reabrir o iniciar una conversación nueva | Smart matching por asunto y destinatarios; unir automáticamente por remitente; reabrir automáticamente al llegar un correo | Smart matching es una configuración de toda la organización en un entorno compartido (RNF-06) y Microsoft no lo recomienda por sus falsos positivos. Unir por remitente mezclaría temas distintos del mismo cliente (tarjeta y préstamo). Negocio pidió decidir a mano la reapertura; el indicador evita que ese correo quede invisible y rompa el 100 % respondido Spike 3: confirmada la correlación por `In-Reply-To` (también con asunto cambiado desde Graph) y que un correo en hilo nuevo queda sin Regarding; confianza alta para Exchange Online. La unión manual se mantiene por los clientes de correo que quitan el encabezado. Spike 4: una persona respondió desde Outlook web con un asunto totalmente distinto y el correo cayó en su conversación por `In-Reply-To`. Los clientes reales escriben desde Gmail, Outlook.com o el celular, que no se probaron: la unión manual sigue siendo la red de seguridad. | RF-01, RF-04, RF-14, RF-15 |
| D-33 | Arreglo en borrador sin número; número provisional impreso en el formato para firma; Formalizar fija el número. Si el spike muestra que la clave alterna (producto, número, arreglo) no admite varios borradores con número vacío, plan B: una columna de texto propia como clave alterna, con el GUID del arreglo mientras es borrador y "producto|número|arreglo" desde que se formaliza | Número al crear; número reservado al imprimir; borradores en una tabla separada | Ver D-09 y D-10. Una tabla separada duplicaría el formato, el calendario y la generación. El plan B mantiene la garantía de unicidad sin depender del tratamiento de nulos de la plataforma Spike 2 (2026-10-08): la clave admite varios nulos y rechaza duplicados con número; el plan B no se usa. | RF-07, RF-16 |
| D-34 | Adjuntos e imágenes con el editor de correo nativo del timeline: adjuntos en Attachment del Email; imágenes pegadas en Base64 dentro del cuerpo del Email (spike 4). Rol de oficial con permisos sobre Attachment; Attachment en la verificación de roles ajenos; todo se purga con el Email. Tamaño y tipos según la configuración de la organización, acordada con el administrador | Componente propio de carga; adjuntos en SharePoint u OneDrive; imágenes en Base64 dentro del cuerpo | El editor nativo ya lo resuelve sin código. SharePoint y OneDrive están prohibidos para datos Sensible (RNF-03). El spike 4 mostró que el editor de correo guarda las imágenes pegadas dentro del cuerpo y no en `msdyn_richtextfiles`, así que no hace falta otorgar esa tabla ni purgarla aparte; el costo es un cuerpo de correo más grande. El límite de adjuntos es de toda la organización en un entorno compartido, así que no lo decide la solución | RF-03, RNF-03, RNF-07, RNF-11 |
| D-35 | Filtro por producto crediticio en las vistas de arreglos: una vista por producto más el filtro de columna nativo del grid | Control de filtro propio; filtro también en conversaciones | Negocio lo pidió solo para arreglos; las conversaciones no tienen producto porque un correo no lo trae. La plataforma lo ofrece sin código | RF-17 |
| D-36 | Moneda del arreglo (decisión de negocio, 2026-10-09): una columna requerida en Arreglo con un choice global de dos opciones, Córdobas y Dólares. Los montos siguen siendo decimales con dos decimales y se interpretan en la moneda del arreglo. Las cuotas heredan la moneda de su arreglo y no llevan columna propia. El choice de campos mapeables incluye "Moneda" para que el formato la imprima | Tipo Moneda de Dataverse; una columna de moneda en cada cuota; dejar todo en córdobas | Hay arreglos en córdobas y en dólares, pero un arreglo nunca mezcla monedas. El tipo Moneda de Dataverse exige tipos de cambio y conversión a moneda base, que el área no necesita. Las cuotas siempre tienen la moneda de su arreglo, así que una columna propia sería un dato duplicado que podría contradecirlo. Queda decidido el 2026-10-09 que una tarjeta con saldo en las dos monedas lleva un solo arreglo, en una sola moneda: el saldo se cordobiza o se dolariza antes de registrarlo (P-22 del diccionario); la clave alterna no cambia | RF-06, RF-08 |

### Supuestos de arquitectura
- Sincronización con Queue y mailbox: el comportamiento real (propietario de los correos, permisos de salida, que la fecha de llegada tomada del mensaje sea correcta, la correlación de respuestas por Regarding) no tiene archivo de referencia dedicado. Se diseñó de forma genérica con los hechos verificados en factibilidad. Confianza baja, se valida en el spike con un buzón de prueba.
- La BU ARP, los teams y la cuenta de servicio dedicada pueden crearse en el entorno compartido, y los usuarios de ARP pueden moverse a ella sin afectar otras soluciones. Confianza baja. Si afecta, plan B: los usuarios quedan en su BU y solo el team propietario está en la BU ARP, a validar contra los roles ajenos con profundidad padre. Ese plan B no es compatible con el team del sistema como propietario (su membresía son los usuarios de la BU, quedaría vacío): si se activa, el propietario pasa a ser un team aparte de membresía manual o de grupo de Entra.
- Un plugin puede consultar la pertenencia de un usuario a un team de grupo de Entra. La membresía se recalcula al acceder al entorno, así que un usuario que no entró todavía podría no figurar. Confianza media. Plan B: team de pertenencia manual.
- Fijar el team como propietario al crear requiere el privilegio Assign. Confianza media.
- Los pipelines de Power Platform están disponibles y hay un host configurado. Confianza baja. Si no, se usa Azure DevOps o GitHub Actions.
- Open XML SDK funciona dentro del sandbox de Dataverse. **Confirmado** en el spike 1b (2026-10-08): el paquete de plugins net462 con DocumentFormat.OpenXml 2.20.0 como ensamblado dependiente se registra y ejecuta, y la salida es idéntica byte a byte a la local para 6, 30 y 60 cuotas. Dentro del plugin: 72 ms en caliente (50 de descarga de la plantilla y 19 de generación) y entre 0,5 y 1 s en frío; tras cada despliegue hay 2 o 3 procesos del sandbox, cada uno con su arranque en frío. El límite de 2 minutos no es un riesgo. Falta abrir el archivo en Excel de escritorio y correr las pruebas unitarias en net462.
- La Custom API devuelve el archivo en un parámetro de salida de texto (Base64). **Confirmado** en el spike 1b: 43,7 KB para 60 cuotas; se probó una salida de 6 MB sin problemas y Microsoft Learn no publica un tope para una salida de texto (los límites más cercanos son de decenas de MB). La Custom API es una acción, no una función, porque escribe la bitácora y el congelamiento.
- Un pago por cuota. **Confirmado el 2026-10-09 por Héctor Romero, vía el usuario.**
- El arreglo pasa a Terminado cuando todas las cuotas tienen pago registrado, aunque sea parcial. **Confirmado el 2026-10-09 por Héctor Romero, vía el usuario.**
- La regla de fechas de vencimiento de cuotas se confirma con Héctor Romero. Confianza baja. El saldo remanente y el total a pagar no dependen de él: los calcula el sistema (ítem 7).
- El arreglo puede no tener conversación de origen, porque puede originarse por teléfono, presencial, en una sucursal o por correo. **Confirmado el 2026-10-09 por Héctor Romero, vía el usuario.**
- El congelamiento al generar el formato para firma asume que el oficial genera el formato solo para imprimir y firmar. Se confirma con Héctor, junto con quién puede reabrir.
- La correlación por `In-Reply-To` deja al correo de respuesta con el mismo Regarding que el correo al que responde. Confirmado en el spike 3 con Exchange Online y en el spike 4 desde Outlook web con el asunto cambiado por una persona; queda sin probar con Outlook de escritorio y con clientes de correo externos (Gmail, celulares), que son los de los clientes reales.
- El botón Responder (timeline y formulario del correo) completa `parentactivityid` en la respuesta. Confirmado en el spike 4 desde la interfaz.
- La clave alterna del arreglo admite varios borradores con número vacío para el mismo crédito. Confirmado en el spike 2.
- Las imágenes pegadas en una respuesta quedan en Base64 dentro del cuerpo del Email (spike 4, con un usuario administrador); con el rol del oficial, sin permisos sobre `msdyn_richtextfiles`, la documentación indica el mismo comportamiento. Falta confirmar el tamaño máximo del cuerpo del correo.
- El tamaño máximo de adjuntos del entorno (5 MB por defecto, en Base64) alcanza para los documentos del área, o el administrador acepta subirlo. Confianza media.
- "Con arreglo de pago" no exige un arreglo formalizado vinculado, y el tiempo total promedio excluye las conversaciones "Sin respuesta". Confianza media; quedaron como preguntas para negocio en el mockup validado.
- Los trabajos de BulkDelete admiten los operadores "older than 24 months" y "no contiene datos" sobre las relaciones. Confianza media, se valida en Test.
- Una respuesta enviada fuera de la solución (por ejemplo desde Outlook) no se detecta. Se mitiga con D-28.
- La reasignación por plugin del propietario de los correos, si la sincronización los deja con otro dueño. Confianza baja.
- La capacidad estimada (1 a 2 GB en el peor caso, 1 a 1,5 KB por fila) no está verificada contra el reporte del tenant. Confianza baja.
- Envío con la Queue como remitente por un usuario no administrador: **confirmado** en el spike 4 con nueve privilegios (ver Seguridad, rol "ARP Oficial"); no hace falta el privilegio de enviar en nombre de otro. Observación: al agregar privilegios a un rol ya asignado, el usuario no los vio hasta quitar y volver a asociar el rol; se tiene en cuenta en los despliegues de roles.
- Las escrituras anidadas del sistema se distinguen de las de un usuario por la identidad de ejecución del plugin. Confianza media.

### Incógnitas abiertas
Ninguna incógnita de `factibilidad-tecnica.md` está marcada bloqueante. Hay un pendiente que bloquea cerrar el diseño de RNF-10: el primero de la lista, que depende de Cumplimiento. Las demás son no bloqueantes.
- **Pendiente de C-02 (bloquea el cierre de RNF-10):** retención y purga de los logs de auditoría de Dataverse, que son del entorno compartido y sobreviven al BulkDelete. Incluye:
  - auditoría de lecturas;
  - si se guarda completo el número de tarjeta (plan B en D-29);
  - si destruir sin archivo a los 2 años es aceptable;
  - la ventana de backups;
  - el riesgo residual del System Administrator.
- Verificación con el administrador de roles ajenos con profundidad Organización o BU padre sobre Email, Queue, Queue Item, Mailbox y las tablas propias (D-18). Condiciona cargar datos reales.
- Quick find sobre la cédula con seguridad por columna, y sobre el número de producto. El spike 2 confirmó que la columna segura se acepta como columna de búsqueda; falta probar que un usuario sin perfil de columna encuentre la fila buscando por cédula (requiere un usuario no administrador). Plan B en D-19.
- RF-08: generador validado en local (spike 1) y dentro del sandbox (spike 1b). Pendiente: aprobación de la v07 por Héctor Romero (C-01) y prueba en Excel de escritorio.
- Inmutabilidad del archivo: si la subida por bloques dispara un step sobre el commit de la subida. La huella ya cubre el caso de que no.
- Comportamiento real de la sincronización con un buzón de prueba: permisos y propietario de la Queue y licencia; Send As de la identidad de sincronización en los entornos de BAC (en el entorno de desarrollo, el envío con la Queue como remitente funcionó: spikes 3 y 4); correlación de respuestas; fecha de llegada; eliminación en cascada de correos, adjuntos y Queue Items con la conversación.
- Pertenencia a team de grupo de Entra consultable por plugin. La concurrencia optimista con ETag quedó confirmada en el spike 2.
- Correlación de respuestas del cliente en la misma conversación por `In-Reply-To`, y qué pasa cuando el cliente cambia el asunto (D-32).
- Clave alterna con número de arreglo vacío: cerrada por el spike 2.
- Permisos de adjuntos (Attachment) para el rol del oficial y tamaño máximo del cuerpo del correo con imágenes pegadas; valor de `MaxUploadFileSize` y extensiones bloqueadas en cada entorno, con el administrador (D-34, RNF-11).
- Columna de fórmula con Now() y operador de fecha relativa: cerradas por el spike 2, con las condiciones de D-27 y D-11.
- Buzón compartido creado con permisos, cuenta de servicio con acceso y acceso al buzón restringido en Exchange (mensajería y TI).
- Si la carpeta Descargas está redirigida a OneDrive (Known Folder Move), con TI o Seguridad.
- Compatibilidad de una clave alterna con una columna con seguridad por columna: refutada por el spike 2 (no se puede en ningún orden); confirma D-07.
- Capacidad de Dataverse (base de datos y archivos) y convivencia en entornos compartidos (registro de ensamblados, pool de requests, host de pipelines, enterprise policy).
- Que Assign sea necesario para crear Cliente y Arreglo con el team como propietario.
- Que el plugin distinga la identidad de ejecución del sistema o de la cuenta de servicio.

## 7. Estimación

### Desglose

<!-- Esperado = (Optimista + 4×Probable + Pesimista) / 6, tolerancia ±0,1. Optimista ≤ Probable ≤ Pesimista, todos ≥ 0. -->
<!-- Base: `referencia: <ítem>` (viene de referencias-esfuerzo.md), `juicio: <rol>` (una persona técnica lo confirmó o ajustó en el checkpoint 3) o `supuesto` (todavía sin confirmar). -->

Unidad: jornadas.

| Entregable | Fase | Requisitos | Optimista | Probable | Pesimista | Esperado | Base |
|---|---|---|---|---|---|---|---|
| Mockup funcional simulado (HTML/React con datos simulados) para validar con negocio antes de los spikes | F1 | RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-08, RF-10, RF-11, RF-12, RF-13 | 3 | 3 | 3 | 3,0 | juicio: Desarrollador de soluciones de automatización |
| Diseño detallado y spike de relleno de plantilla con Open XML SDK (F-NIC-0000052, bloque de 60 cuotas) | F1 | RF-06, RF-08, RF-09 | 2 | 3 | 4 | 3,0 | referencia: Diseño detallado y spike de parseo de Excel en Custom API |
| Spike de server-side synchronization (Queue, Send As, propietario, correlación por `In-Reply-To`, adjuntos e imágenes del editor) y de quick find con seguridad por columna | F1 | RF-01, RF-03, RF-13, RF-14, RNF-08, RNF-11 | 1,5 | 2,5 | 5 | 2,75 | juicio: Desarrollador de soluciones de automatización |
| Núcleo: modelo de datos propio, roles, plugins de reglas de servidor (incluidos borrador, formalización con número provisional y descarte) y app model-driven base | F1 | RF-02, RF-05, RF-06, RF-07, RF-10, RF-11, RF-12, RF-16, RF-17, RNF-04 | 6 | 8 | 12 | 8,33 | juicio: Desarrollador de soluciones de automatización |
| Conversaciones de correo: BU ARP, teams, Queue con mailbox, plugin de registro de correos en conversaciones, registro de respuestas y tiempos (por respuesta, primera y total), finalización y correo en finalizadas, adjuntos e imágenes, reconciliación y flujos de monitoreo | F1 | RF-01, RF-02, RF-03, RF-04, RF-15, RNF-08, RNF-11 | 3 | 5 | 8 | 5,17 | juicio: Desarrollador de soluciones de automatización |
| Custom API de generación del formato y generador Open XML (huella, mapeo, bloque de cuotas, bitácora, congelamiento e instantánea) | F1 | RF-08, RF-06 | 3 | 5 | 8 | 5,17 | juicio: Desarrollador de soluciones de automatización |
| Administración de plantilla y mapeo (versiones, publicación con validación, inmutabilidad, vista previa) | F1 | RF-09 | 1 | 2 | 4 | 2,17 | juicio: Desarrollador de soluciones de automatización |
| Custom API "Tomar" conversación, reconciliación de correos, unir conversaciones e iniciar conversación nueva | F1 | RF-02, RF-01, RF-14, RF-15 | 2 | 3 | 4 | 3,0 | juicio: Desarrollador de soluciones de automatización |
| Retención: cuatro trabajos BulkDelete (incluidas las imágenes del editor), rol ARP Purga, vista de borradores sin movimiento y ensayo de corrida | F1 | RNF-07, RNF-10, RF-16 | 1 | 2,5 | 4 | 2,5 | juicio: Desarrollador de soluciones de automatización |
| Diseño de pantallas model-driven (sitemap, vistas por perfil y por producto, formularios, dashboard, gráficos, command bar con web resource JS), siguiendo el mockup validado | F1 | RF-03, RF-04, RF-11, RF-13, RF-14, RF-15, RF-16, RF-17 | 2 | 4 | 6 | 4,0 | juicio: Desarrollador de soluciones de automatización |
| Aislamiento y seguridad: verificación de roles ajenos, perfil de columna, auditoría, evidencia para Cumplimiento | F1 | RNF-03, RNF-04, RNF-10 | 1 | 2 | 4 | 2,17 | juicio: Desarrollador de soluciones de automatización |
| Gestión del proyecto | F1 | RNF-09 | 1 | 2 | 3 | 2,0 | juicio: Desarrollador de soluciones de automatización |
| Pruebas y QA | F1 | RF-01, RF-13, RF-14, RF-15, RF-16, RNF-02, RNF-08 | 2 | 4 | 6 | 4,0 | juicio: Desarrollador de soluciones de automatización |
| Entornos y despliegue | F1 | RNF-06 | 1 | 2 | 4 | 2,17 | juicio: Desarrollador de soluciones de automatización |
| Capacitación | F1 | RNF-01 | 0,5 | 1 | 1,5 | 1,0 | juicio: Desarrollador de soluciones de automatización |
| Gestión del cambio | F1 | RNF-01 | 0,5 | 1 | 2 | 1,08 | juicio: Desarrollador de soluciones de automatización |
| Soporte post salida | F1 | RNF-09 | 1 | 2 | 3 | 2,0 | juicio: Desarrollador de soluciones de automatización |

### Resumen

esfuerzo_esperado: 53,5
contingencia: 30%
confianza: alta

Cálculo de confianza: todas las filas tienen base `referencia:` o `juicio:` (53,51 jornadas), p = 1,00 ≥ 0,70 → confianza alta. Las siete filas que cambiaron el 2026-10-08 por la validación del mockup las reestimó el arquitecto y las confirmó tal cual el Desarrollador de soluciones de automatización el 2026-10-09.

Historia: la estimación aprobada el 2026-10-07 era de 46,9 jornadas con confianza alta. Los cambios de la validación del mockup suman 6,6 jornadas esperadas.

Contingencia de 30 % (16,1 jornadas, total de unas 70): cuatro riesgos media/alto o alta/alto de la sección 9 sin mitigación firme. Son el plazo con fase única, la fidelidad del relleno de la plantilla y la v07 de Héctor Romero, el comportamiento real de la server-side synchronization, que ahora incluye la correlación de conversaciones (plan B: volver a C1), y las dependencias de terceros (buzón, BU, roles ajenos, configuración de adjuntos, Cumplimiento). Suman la cantidad de reglas de servidor, la adopción sin Outlook y los entornos compartidos. El riesgo de cambios de negocio al ver el mockup ya se materializó y está absorbido en esta estimación, por eso la contingencia no sube.

## 8. Plan y roles requeridos

duracion_meses: 2,5

Cálculo: 53,5 jornadas ÷ (1 persona × 100 %) ÷ 21 jornadas por mes (supuesto) = 2,5 meses; con contingencia, unos 3,3 meses. Dentro de la talla M (más de 1 y hasta 3 meses), sin desvío; con contingencia pasaría a L. Choca con RNF-09: el 2026-10-31 son unos 17 días hábiles, así que se resuelve en C-03 con Deyanira Ramírez.

Orden de trabajo dentro de F1: el mockup funcional simulado ya está validado por negocio (2026-10-08, versión 3). Siguen los spikes de plantilla y de sincronización (que ahora incluye correlación de conversaciones, adjuntos e imágenes, y la clave alterna con borradores); después la construcción.

| Rol | Perfil | Dedicación |
|---|---|---|
| Desarrollador de soluciones | Desarrollador de soluciones de automatización (Dataverse, plugins C#, model-driven, Open XML), 1 persona | 100 % |
| Validación de negocio | Deyanira Ramírez y Héctor Romero: validación del mockup; Héctor además aprueba la plantilla v07 y las reglas de cuotas (puntual, no entra en el cálculo) | Puntual |
| Administrador y mensajería | Administrador del entorno y mensajería: BU, roles ajenos, Send As, buzones y pipelines (puntual, no entra en el cálculo) | Puntual |

## 9. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación | Responsable |
|---|---|---|---|---|
| Plazo: 53,5 jornadas esperadas (unas 70 con contingencia) contra unos 16 días hábiles al 2026-10-31, con fase única y una persona. **Materializado:** el 2026-10-09 Deyanira Ramírez confirmó la fecha sin recorte ni fases | alta | alto | Construir en orden de valor, de modo que lo terminado al 2026-10-31 se pueda usar aunque no esté todo (primero el control de correos, que es el disparador del proyecto); medir el avance real al cerrar el modelo de datos y, si confirma la brecha, volver a Deyanira Ramírez con datos y una propuesta concreta de qué queda para después del 31 | Desarrollador de soluciones de automatización |
| Cambios de negocio al ver el mockup que muevan requisitos o arquitectura | media | alto | Se materializó el 2026-10-08: conversaciones, finalización con resultado, borradores, adjuntos y filtro por producto. Se incorporaron a esta definición antes de construir; el mockup v3 quedó validado. Cambios posteriores siguen el mismo camino | Desarrollador de soluciones de automatización |
| Correlación de conversaciones distinta de la esperada (respuestas que no quedan en su conversación, clientes que cambian el asunto) | media | medio | Spike con buzón de prueba; plan B de búsqueda por `In-Reply-To` en el plugin; unión manual (D-32) | Desarrollador de soluciones de automatización |
| Imágenes del editor y adjuntos con datos Sensible fuera del alcance del aislamiento o de la purga | media | alto | Incluir Attachment en la verificación de roles ajenos; las imágenes pegadas viven dentro del Email y se purgan con él (spike 4, D-34) | Desarrollador de soluciones de automatización |
| Límite de adjuntos del entorno compartido insuficiente para el área | media | bajo | Acordar `MaxUploadFileSize` y extensiones con el administrador antes de Prod (RNF-11) | Administrador de Power Platform |
| Borradores olvidados que nunca se formalizan ni descartan | media | bajo | Vista de borradores sin movimiento para el jefe (D-22) | Héctor Romero |
| Fidelidad del relleno de la plantilla controlada F-NIC-0000052 con Open XML en sandbox | media | alto | Spike temprano con la plantilla real; plan B generación en JavaScript en el cliente (D-13); huella SHA-256 y versionado (D-15) | Desarrollador de soluciones de automatización |
| Plantilla v07 con 60 cuotas y reglas de cuotas sin aprobar a tiempo | media | alto | Pedir la v07 y las reglas (fechas de vencimiento, validaciones numéricas, cuándo pasa a Terminado) junto con la validación del mockup (C-01) | Héctor Romero |
| Comportamiento real de la server-side synchronization (permisos, Send As, propietario de la Queue, fecha de llegada) | media | alto | Spike con buzón de prueba en Dev; plan B volver a la alternativa C1 (flujo sobre el buzón compartido) con revisión de diseño | Desarrollador de soluciones de automatización |
| Normativa externa (SIBOIF, sigilo bancario) que cambie auditoría, retención de logs, número de tarjeta o destrucción a los 2 años | media | alto | Consulta formal a Cumplimiento antes de cerrar el diseño de datos (C-02); plan B de enmascarado del número de tarjeta (D-29) | Deyanira Ramírez |
| Lectura de datos Sensible por roles de otras soluciones en el entorno compartido, y riesgo residual del System Administrator | media | alto | BU dedicada ARP, verificación de roles ajenos con el administrador antes de cargar datos reales y antes de cada pase a Prod, riesgo residual declarado a Cumplimiento (D-18) | Administrador de Power Platform |
| Dependencias de terceros: buzón compartido, Send As, BU, teams, host de pipelines | media | medio | Pedirlos al inicio, en paralelo con el mockup | Desarrollador de soluciones de automatización |
| Los Excel existentes con datos "Sensible" siguen en SharePoint | alta | medio | Plan escrito de restricción de uso o retiro (C-04) | Héctor Romero |
| Adopción: oficiales sin Outlook que responden solo desde la solución; asignación formal en lugar de "levantar la mano" | media | medio | Validación del mockup con el área, capacitación y gestión del cambio planificadas | Héctor Romero |
| Cantidad de reglas de servidor (plugins y 3 Custom API) con errores sutiles (solo-sistema, congelamiento, consecutivo) | media | medio | Pruebas unitarias de plugins y pruebas manuales por entorno en Pruebas y QA | Desarrollador de soluciones de automatización |
| Fallas silenciosas en la captura de correos que rompan el "100 % respondido" | media | medio | Flujos de monitoreo del mailbox y de salud cada 30 minutos con reconciliación (D-21) | Desarrollador de soluciones de automatización |
| Error en el mapeo editable que imprima datos en la celda equivocada de un documento firmado | baja | alto | Validación al publicar, vista previa, inmutabilidad y auditoría del mapeo (D-15) | Héctor Romero |
| Documento descargado con datos Sensible en la estación del oficial, posible redirección de Descargas a OneDrive | media | medio | Consultar a TI si Descargas está redirigida (Known Folder Move) | Desarrollador de soluciones de automatización |
| Entornos compartidos: registro de ensamblados, pool de requests, buzones de prueba por entorno | media | medio | Coordinación con el administrador; buzones de prueba propios en Dev y Test (D-23) | Administrador de Power Platform |
| Capacidad de Dataverse (base de datos y archivos por adjuntos) no verificada | baja | medio | Reporte de capacidad del tenant antes de Prod | Administrador de Power Platform |

## 10. Supuestos y dependencias

Supuestos:

- Un pago parcial registrado en una cuota la saca de la lista de riesgo, porque el criterio de RF-11 es solo por fecha y por existencia de pago registrado [fuente: elicitación].
- Las conversaciones que no terminan en arreglo se conservan con el mismo criterio de 2 años, contados desde su finalización.
- Los oficiales responden siempre desde la solución y no desde su Outlook personal [fuente: elicitación].
- El equipo tiene capacidad en C#/.NET y en React/TypeScript [fuente: elicitación].
- 21 jornadas hábiles por mes y una persona al 100 % para calcular la duración.
- Los supuestos de arquitectura de la sección 6 que siguen abiertos (regla de fechas de vencimiento, número provisional, "Con arreglo de pago" sin arreglo formalizado) se confirman con Héctor Romero; quedaron como preguntas dentro del mockup validado el 2026-10-08. Cuándo pasa a Terminado, un pago por cuota y la conversación de origen opcional se confirmaron el 2026-10-09 (por Héctor Romero, vía el usuario).

Dependencias:

- Deyanira Ramírez y Héctor Romero validan el mockup funcional simulado antes de los spikes. Cumplida el 2026-10-08 (versión 3).
- El administrador de Power Platform confirma en cada entorno el tamaño máximo de adjuntos y las extensiones bloqueadas (RNF-11).
- Mensajería o TI crea el buzón compartido del área, que reemplaza al grupo de distribución [fuente: ficha §9].
- El administrador de Power Platform y Exchange habilita la server-side synchronization en los entornos [fuente: factibilidad-tecnica §7].
- Héctor Romero aprueba la plantilla v07 del F-NIC-0000052 (C-01) y el plan para los Excel en SharePoint (C-04).
- Cumplimiento responde sobre la SIBOIF y el sigilo bancario (C-02).
- Deyanira Ramírez confirma por escrito la fecha, un recorte o fases después de la estimación (C-03).

## 11. Aprobación

- resultado: aprobada
- rol: Desarrollador de soluciones de automatización
- fecha: 2026-10-07
- comentarios: Actualizada el 2026-10-08 con los cambios de la validación del mockup: conversaciones (RF-01, RF-03, RF-04, RF-14, RF-15), borrador y formalización (RF-07, RF-16), filtro por producto (RF-17), adjuntos e imágenes (RNF-11) y decisiones D-03, D-05, D-06, D-09, D-10, D-17, D-22, D-25, D-26, D-27, D-30, D-31 y D-32 a D-35. La edición la hizo el arquitecto directamente, por decisión del usuario, sin pasar por revisor-arquitectura ni por el checkpoint de estimación. Pendiente: que el usuario revise los cambios y confirme las siete filas de estimación en supuesto.
