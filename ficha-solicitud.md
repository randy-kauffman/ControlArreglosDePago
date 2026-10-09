# Ficha de solicitud: Control de Arreglos de Pago

<!-- Convención: cada dato tomado del material o de la entrevista cita su origen en línea, con el formato [fuente: <origen>]. Ejemplo: "Hoy tardan 3 días en consolidar el reporte [fuente: entrevista solicitante]". -->

## 1. Metadatos

- id: 2026-003-control-arreglos-pago
- fecha: 2026-10-07
- actualizado: 2026-10-07
- tipo: interno
- panorama: bac-credomatic/arreglos-de-pago
- estado: completa

## 2. Solicitante y actores

<!-- estado-seccion: completa -->

- Tipo y área: proyecto interno de BAC Credomatic, del departamento de Arreglos de Pago (ARP, departamento completamente nuevo) del área de Cobranza [fuente: Triage del solicitante, 2026-10-07].
- Solicitante y decisora final: Deyanira Ramírez, Vice Gerente de Cobranza (jefa máxima del área). Ella hizo el pedido y toma la decisión de si avanza [fuente: entrevista, 2026-10-07].
- Referente de procesos: Héctor Romero, jefe de Arreglos de Pago. Participa por ser el jefe del área donde se implementa el proyecto y quien más conoce los procedimientos [fuente: entrevista, 2026-10-07].
- Cadena superior (no participa en la decisión): VP de Banca de Personas y Medios de Pago, Juan Ignacio Baltodano; luego el Country Manager, Juan Carlos Sanson [fuente: Triage del solicitante, 2026-10-07].
- Usuarios: todas las personas del área de ARP, principalmente los Oficiales de Arreglo de Pagos [fuente: entrevista, 2026-10-07].
- Otros afectados fuera del departamento: ninguno [fuente: entrevista, 2026-10-07].

## 3. Origen

<!-- estado-seccion: completa -->

- El pedido lo hizo Deyanira Ramírez, Vice Gerente de Cobranza [fuente: entrevista, 2026-10-07].
- Disparador: quejas de clientes porque los correos en que solicitan arreglos de pago no se responden en tiempo y forma [fuente: entrevista, 2026-10-07].
- Contexto: Arreglos de Pago es un departamento completamente nuevo y lo único que existe hoy para el proceso es un formato manual en Excel [fuente: Triage del solicitante, 2026-10-07].
- Título pedido: "Control de Arreglos de Pago" [fuente: Triage del solicitante, 2026-10-07].

## 4. Problema

<!-- estado-seccion: completa -->

- Palabras del solicitante: "No tenemos control y seguimiento de los correos electrónicos que llegan solicitando arreglos de pagos; no sabemos si ya le respondimos, qué le respondimos, cuánto tardamos en responder" [fuente: entrevista, 2026-10-07].
- Palabras de Héctor Romero (referente de procesos): no tienen un control robusto de todos los arreglos de pago viejos y activos que un cliente puede tener; hoy son Exceles dispersos [fuente: entrevista, 2026-10-07].
- Resumen en una frase: el área de Arreglos de Pago no puede seguir las solicitudes de arreglo que llegan por correo (si se respondieron, qué se respondió y en cuánto tiempo) ni ver de forma consolidada los arreglos vigentes e históricos de cada cliente, que hoy viven en archivos Excel dispersos; esto genera quejas de clientes por respuestas tardías o incompletas [fuente: entrevista, 2026-10-07].
- Evidencia complementaria (observación del análisis del formato, no dicho por el solicitante): ver los dolores de la sección 5.

## 5. Situación actual

<!-- estado-seccion: completa -->

Pasos AS-IS [fuente: entrevista, 2026-10-07, salvo indicación]:

1. El cliente envía un correo solicitando un arreglo de pago sobre un préstamo o una tarjeta de crédito determinados. El correo llega a un grupo de distribución (no a un buzón compartido), así que le cae a cada uno de los oficiales.
2. Asignación informal: un oficial "levanta la mano" y avisa que toma el caso. No hay registro de quién tomó cada solicitud.
3. El oficial recopila los saldos manualmente en el sistema AS400 que corresponda: NIC400C para préstamos, NIC400B para tarjetas.
4. El oficial llena el formulario Excel F-NIC-0000052 y le ofrece al cliente una solución de arreglo de pago.
5. Si el cliente acepta, el formato se imprime y el cliente tiene que presentarse a firmarlo en el área de Arreglos de Pago o en una sucursal bancaria. Firman el cliente y el oficial [fuente: Formulario F-NIC-0000052 v06 (Excel), hoja FORMATO ARP ELECTRONICO, celdas A45 y E45].
6. Pago de cuotas: es manual por parte del cliente, que paga en una sucursal bancaria o por la web. No hay débito automático.
7. Seguimiento: en cada fecha de cuota que fija el cronograma, el oficial revisa si el cliente pagó, cuándo y cuánto, para saber si cumple el arreglo. Lo consulta en NIC400B si es tarjeta o en NIC400C si es préstamo, y lo anota en la hoja "CONTROL DE PAGOS" del Excel del arreglo, que funciona como historial.
8. Incumplimiento: no ocurre nada automáticamente. Si el oficial ve que el cliente no está pagando, puede anular el arreglo y la gestión de cobro sigue por el departamento de Cobranza.
9. Almacenamiento: los Excel de cada arreglo se guardan en un SharePoint.
10. Consulta de arreglos previos de un cliente: el oficial busca en el SharePoint o en el AS400, donde normalmente se deja una nota en el préstamo o la tarjeta indicando que tuvo arreglo de pago.

Reglas del dato [fuente: entrevista, 2026-10-07]:

- Tipos de arreglo: Temporal (el arreglo da un alivio temporal pero no termina de pagar la deuda) y Cancelación (con el arreglo el cliente termina de pagar completamente el crédito).
- Cuota: la calcula el oficial a mano, a partir de la deuda y de lo que negoció con el cliente que puede pagar. No hay fórmula de política.
- Plazo (cantidad de cuotas): es el tiempo total en que el arreglo queda vigente. Hay arreglos de más de 36 cuotas, hasta 60.
- Número de Arreglo de Pagos: es un consecutivo por crédito que indica cuántas veces ese préstamo o tarjeta tuvo arreglo de pago; un crédito puede tener más de un arreglo.
- Formato único: F-NIC-0000052 se usa tanto para préstamos como para tarjetas; no hay otro formato.
- Respuesta al cliente: no hay plantillas; cada oficial redacta su respuesta.

Volumen: unos 80 correos de solicitud por semana, de los que unos 60 terminan en un arreglo, atendidos por 3 oficiales [fuente: entrevista, 2026-10-07].

Herramientas: Excel (formato manual), SharePoint (almacenamiento de los Excel), correo con grupo de distribución, NIC400C y NIC400B en AS400 [fuente: Triage del solicitante y entrevista, 2026-10-07].

Estructura del formato [fuente: Formulario F-NIC-0000052 v06 (Excel)]:

- Hoja "FORMATO ARP ELECTRONICO": formalización con fecha, oficial, nombre del cliente, cédula, correo, celular (celdas A7 a F9). Datos del arreglo: número de préstamo, "Tipo de Arreglo T/C", número de arreglo, saldo inicial, monto abonado a la fecha, saldo remanente, plazo en meses, cuota mensual, total a pagar, fecha de inicio, fecha de finalización y número de cuenta a debitar (A12 a E15). Incluye comentarios y cláusulas de autorización de débito.
- Hoja "CONTROL DE PAGOS": calendario de hasta 48 cuotas con fecha de cuota, monto, fecha de pago y monto pagado (A6 a E54), y totales en la fila 55.

Dolores dichos por el solicitante [fuente: entrevista, 2026-10-07]:

- No hay control ni seguimiento de los correos de solicitud: no se sabe si se respondió, qué se respondió ni cuánto se tardó. Esto genera quejas de clientes.
- La asignación es informal (alguien avisa que toma el caso), sobre un grupo de distribución donde cada oficial recibe todos los correos.
- Para supervisar que todos los correos se hayan respondido, Héctor Romero tiene que revisar correo por correo.
- No hay un control robusto de los arreglos viejos y activos de un cliente: son Exceles dispersos.
- El seguimiento de cumplimiento es manual: el oficial tiene que acordarse de revisar en cada fecha de cuota, y la detección de incumplimientos depende de que lo haga.

Dolores (observaciones del análisis del formato, no dichos por el solicitante):

- La tabla de "CONTROL DE PAGOS" tiene 48 cuotas (filas 7 a 54), pero los totales C55 y E55 usan `=SUM(C7:C42)` y `=SUM(E7:E42)`, es decir, solo las cuotas 1 a 36. Como existen arreglos de hasta 60 cuotas [fuente: entrevista, 2026-10-07], el Excel totaliza mal desde la cuota 37 y ni siquiera tiene filas para las cuotas 49 a 60 [fuente: Formulario F-NIC-0000052 v06 (Excel), hoja CONTROL DE PAGOS, celdas C55 y E55].
- "Fecha Formalización" (B7) y "Fecha" del calendario (E4) usan `=TODAY()`, así que la fecha de formalización cambia cada vez que se abre el archivo [fuente: Formulario F-NIC-0000052 v06 (Excel), hojas FORMATO ARP ELECTRONICO y CONTROL DE PAGOS, celdas B7 y E4].
- F7 trae precargado el nombre de un oficial ("Roger Moran"), que puede quedar en formatos de otros oficiales si no se cambia [fuente: Formulario F-NIC-0000052 v06 (Excel), hoja FORMATO ARP ELECTRONICO, celda F7].
- El formato es de "Arreglo de Pagos Préstamos", pero tiene campos de tarjeta de crédito, como "Tipo de Arreglo T/C", y las cláusulas hablan de tarjetas y préstamos. El título A4 y la etiqueta "Número de Préstamo" no distinguen el producto [fuente: Formulario F-NIC-0000052 v06 (Excel), hoja FORMATO ARP ELECTRONICO, celdas A4, A12 y C12].
- Los campos de seguimiento de pagos (fecha de pago, monto pagado) están en una hoja del mismo archivo, que se llena a mano [fuente: Formulario F-NIC-0000052 v06 (Excel), hoja CONTROL DE PAGOS, celdas D6 y E6].

Observaciones del análisis (no dichas por el solicitante):

- Los Excel guardados en SharePoint contienen nombre, cédula, correo, celular y cuenta a debitar del cliente, que el solicitante califica como datos super sensibles [fuente: entrevista, 2026-10-07]. El panorama prohíbe almacenar datos sensibles en SharePoint [fuente: panorama bac-credomatic/_empresa, §7].
- La cláusula del formato dice "autorizo a BAC a debitar de mi cuenta… las cuotas pactadas", pero en la práctica el pago lo hace el cliente a mano [fuente: Formulario F-NIC-0000052 v06 (Excel), hoja FORMATO ARP ELECTRONICO, celda A25; entrevista, 2026-10-07].

## 6. Resultado esperado

<!-- estado-seccion: completa -->

Línea base [fuente: entrevista, 2026-10-07]:

- Tiempo de respuesta a un correo de solicitud: unas 3 horas en promedio. El solicitante aclara que el tiempo no es el problema principal.
- El problema principal es que hay correos que nunca se responden. Hoy no se mide cuántos.
- Quejas de clientes por correos sin responder: pocas, a lo mucho 2 o 3 por mes. Lo importante, según el solicitante, es que existan.

Criterios de éxito [fuente: entrevista, 2026-10-07]:

1. El 100 % de los correos de solicitud de arreglo reciben respuesta.
2. Los tiempos de respuesta de cada correo son visibles y medibles.
3. Cero quejas de clientes por correos de solicitud sin responder.
4. El jefe del área puede comprobar que todos los correos se respondieron sin revisarlos uno por uno.
5. El área cuenta con una lista de los arreglos en riesgo (cuotas cuya fecha venció sin pago registrado) para revisarlos. No se espera que el sistema avise al cliente: pagar a tiempo es responsabilidad del cliente.

6. Un oficial puede buscar por cédula del cliente o por número de préstamo o tarjeta y ver al instante todos los arreglos de ese cliente, vigentes e históricos, con su estado, sin recorrer el SharePoint ni las notas del AS400.

## 7. Alcance preliminar

<!-- estado-seccion: completa -->

Incluido, todo imprescindible [fuente: entrevista, 2026-10-07]:

1. Recepción de solicitudes por correo: entrada, asignación a un oficial, respuesta al cliente y medición de tiempos.
2. Formalización: consulta de saldos según el producto, carga de los datos del arreglo dentro de la solución e impresión del formato F-NIC-0000052 con esos datos para que el cliente lo firme. La posición en que se imprime cada valor sobre el formato debe ser parametrizable sin tocar código, para absorber cambios del formato (por ejemplo, de leyendas legales) [fuente: entrevista, 2026-10-07].
3. Seguimiento de cuotas: registro de pagos y lista de arreglos en riesgo.
4. Anulación del arreglo por parte del oficial.
5. Historial por cliente: búsqueda por cédula o por número de préstamo o tarjeta.

Productos: préstamos y tarjetas de crédito [fuente: Triage del solicitante, 2026-10-07].

Excluido [fuente: entrevista, 2026-10-07]:

- Todo el flujo físico: la firma presencial del formato por el cliente (en el área o en una sucursal) y el pago manual de las cuotas por parte del cliente.
- Migración de los Excel de arreglos existentes en SharePoint: no se migran; el control arranca con los arreglos nuevos.

## 8. Restricciones

<!-- estado-seccion: completa -->

Las restricciones marcadas con fuente panorama no fueron dichas por el solicitante; se confirman en la relectura.

- Plataforma: Power Platform es obligatoria para automatizaciones, con Dataverse como base de datos [fuente: panorama bac-credomatic/_empresa].
- Licenciamiento: todos los usuarios del área (los 3 oficiales y el jefe) tienen Power Apps Premium per user [fuente: entrevista, 2026-10-07].
- Integración con el core: Power Platform no puede conectarse a AS400. Toda interacción con el core (NIC400C y NIC400B) pasa por bots de RPA de Automation Anywhere [fuente: panorama bac-credomatic/_empresa]. Los saldos de préstamos tienen como fuente de verdad NIC400C y los de tarjetas NIC400B [fuente: panorama bac-credomatic/cobranza].
- Segregación de funciones: los cambios digitados a mano en AS400 requieren aprobación de un supervisor distinto de quien digita [fuente: panorama bac-credomatic/_empresa].
- Datos sensibles: está prohibido almacenarlos en SharePoint u OneDrive. Deben vivir en Dataverse u otro almacén aprobado [fuente: panorama bac-credomatic/_empresa]. Nota del análisis del formato: el formato maneja nombre, cédula, correo, celular y número de cuenta a debitar del cliente [fuente: Formulario F-NIC-0000052 v06 (Excel), hoja FORMATO ARP ELECTRONICO, celdas A8 a E9 y E15]. La clasificación formal de datos sensibles y las regulaciones bancarias aplicables (por ejemplo SIBOIF) siguen sin datos en el panorama [fuente: panorama bac-credomatic/_empresa].
- Presupuesto: los proyectos internos salen con las licencias y recursos existentes, sin presupuesto adicional por defecto [fuente: panorama bac-credomatic/_empresa].
- Entornos: un Dev, un Test y un Prod para toda Nicaragua, compartidos con otras soluciones [fuente: panorama bac-credomatic/_empresa].
- Retención de datos: no hay política rígida, el plazo se define por proyecto [fuente: panorama bac-credomatic/_empresa].
- Fecha límite: entrega al final de octubre de 2026 (2026-10-31) [fuente: entrevista, 2026-10-07].
- Sin integración con el core: el oficial sigue consultando a mano los saldos en NIC400B/NIC400C y registra a mano dentro de la solución los pagos de cada cuota [fuente: entrevista, 2026-10-07].
- Datos super sensibles: nombre, cédula, correo, celular y número de cuenta del cliente son datos super sensibles según el solicitante, con clasificación formal "Sensible" [fuente: entrevista, 2026-10-07]. Por la regla del panorama no pueden almacenarse en SharePoint ni OneDrive [fuente: panorama bac-credomatic/_empresa]. Observación del análisis: la práctica actual (Excel con esos datos en SharePoint) choca con esa regla.
- Formato controlado: F-NIC-0000052 es un documento controlado cuyo dueño es Héctor Romero; lo que se imprima para firma debe respetarlo [fuente: entrevista, 2026-10-07].

## 9. Solución sugerida

<!-- estado-seccion: completa -->

- (no validado) Reemplazar el grupo de distribución por un buzón compartido del área; el solicitante indicó que el cambio es posible [fuente: entrevista, 2026-10-07].
- Fuera de eso, el solicitante no trae una solución: se acercó precisamente porque no sabe cómo resolverlo y espera una recomendación del equipo de automatización [fuente: entrevista, 2026-10-07].
- Alternativa consultada y descartada por el área: resolverlo con flujos en el CRM corporativo. Se descartó porque los flujos en el CRM son lentos y poco personalizables, y porque el CRM está en una migración que se extiende hasta 2027, mientras que el área necesita resolverlo lo antes posible [fuente: entrevista de validación, 2026-10-07].

## 10. Supuestos y preguntas abiertas

<!-- estado-seccion: completa -->

Supuestos (sin confirmar):

- La gestión de cobro que sigue después de anular un arreglo es de Cobranza y queda fuera del alcance.
- Los arreglos anteriores a la puesta en marcha se siguen consultando como hoy (SharePoint y notas del AS400), ya que no se migran.

Preguntas abiertas:

1. ¿Hay regulación bancaria externa (por ejemplo, normas de la SIBOIF o el sigilo bancario) que imponga requisitos específicos sobre estos datos, como auditoría de accesos, retención mínima o ubicación de los datos? La clasificación interna ya se conoce: "Sensible" [fuente: entrevista, 2026-10-07].
