using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Sanic.Carp.Plugins.Dominio.Calendario;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Dominio.Plantilla
{
    /// <summary>
    /// Una fila de la tabla Mapeo de celda tal como la necesita la validación de publicación (diseno/03 2.0.7). Es un tipo del dominio
    /// y no el <c>MapeoCelda</c> del generador (<c>Formato/</c>, aún no portado, 4.3): el dominio no puede depender de Dataverse y la
    /// dependencia <c>Dominio → Formato</c> de 4.1 se pospone hasta que ese código exista. Se usan los choices de <c>Choices.g.cs</c>.
    /// </summary>
    public sealed class FilaDeMapeo
    {
        /// <summary>Nombre exacto de la hoja, tal cual (puede empezar con un espacio, spike 1 hallazgo 1). No se recorta.</summary>
        public string Hoja { get; }

        /// <summary>Celda en notación A1 (la de la primera cuota, para un bloque).</summary>
        public string Celda { get; }

        public TipoDeMapeo Tipo { get; }
        public CampoMapeable Campo { get; }

        /// <summary><c>sanic_filasmaximas</c>; solo lo llevan las columnas de bloque.</summary>
        public int? FilasMaximas { get; }

        public FilaDeMapeo(string hoja, string celda, TipoDeMapeo tipo, CampoMapeable campo, int? filasMaximas = null)
        {
            Hoja = hoja;
            Celda = celda;
            Tipo = tipo;
            Campo = campo;
            FilasMaximas = filasMaximas;
        }
    }

    /// <summary>Validación de publicación de una versión de plantilla (diseno/03 2.0.7; D-15, def. ítem 15).</summary>
    public static class ValidadorDePublicacion
    {
        /// <summary>
        /// Devuelve TODOS los errores, no solo el primero; lista vacía si el mapeo se puede publicar (diseno/03 2.0.7).
        /// <list type="number">
        /// <item>Mapeo nulo o vacío ⇒ <c>CARP-VER-007</c>.</item>
        /// <item>Lista cerrada de reglas del generador (<c>ValidarMapeo</c> del spike 1, <c>ValidadorMapeo.cs</c>), un
        /// <c>CARP-VER-007</c> por problema: hoja indicada y celda A1 bien formada, sin dos campos en la misma celda de la misma hoja,
        /// campo coherente con el tipo (una fila con un problema de hoja, celda o coherencia da un solo error y no entra en las reglas
        /// siguientes); y las del bloque de cuotas: una sola hoja (si no, es el único error del bloque), ninguna columna repetida, bloque
        /// completo, misma fila inicial, filas máximas entre 1 y 60, el bloque no se sale de la hoja y ninguna otra celda lo pisa.</item>
        /// <item>Tabla de compatibilidad de dicc. §3.1 (columna 4), como lista blanca (BP-PP-062): las 19 primeras opciones y
        /// <c>Moneda</c> solo con ValorSimple; <c>Cuota: …</c> (5) solo con ColumnaDeBloqueDeCuotas; <c>Total: …</c> (2) solo con
        /// TotalCalculado. Una opción que el enum no define no es compatible con ningún tipo ⇒ <c>CARP-VER-007</c>.</item>
        /// <item>Las filas máximas de las columnas del bloque no coinciden con <paramref name="capacidad"/> ⇒ UN solo error
        /// <c>CARP-VER-008</c> (el texto no nombra la columna). Las filas máximas distintas entre columnas no son un 007 aparte: las
        /// cubre esta regla.</item>
        /// </list>
        /// Cada error de la regla 1 a 3 es <c>CodigosDeError.VER_007</c> con el problema concreto en <c>{errores}</c>; el de la regla 4,
        /// <c>CodigosDeError.VER_008</c> con la capacidad.
        /// </summary>
        public static IReadOnlyList<ErrorDeNegocio> Validar(int capacidad, IReadOnlyList<FilaDeMapeo> filasDeMapeo)
        {
            var errores = new List<ErrorDeNegocio>();

            if (filasDeMapeo == null || filasDeMapeo.Count == 0)
            {
                // Sin filas no hay bloque que comparar con la capacidad: solo el 007 (PA-PUB-02). Una lista nula se trata como vacía.
                errores.Add(CodigosDeError.VER_007.Crear("el mapeo no tiene ninguna fila"));
                return errores;
            }

            var validas = new List<FilaValida>();
            for (int i = 0; i < filasDeMapeo.Count; i++)
            {
                FilaValida valida = ValidarFila(i + 1, filasDeMapeo[i], errores);
                if (valida != null) validas.Add(valida);
            }

            ValidarCeldasRepetidas(validas, errores);
            ValidarBloque(validas, errores);
            ValidarCapacidad(capacidad, filasDeMapeo, errores);
            return errores;
        }

        // ---- Regla 2 (portada de ValidarMapeo del spike 1) y regla 3 (tabla de compatibilidad de dicc. §3.1, columna 4) ----

        private sealed class FilaValida
        {
            public FilaDeMapeo Fila;
            public int Columna;
            public int NumeroDeFila;
        }

        // Una fila con un problema de hoja, de celda o de compatibilidad da UN error y no sigue a las reglas que miran el conjunto
        // (celdas repetidas, bloque completo): igual que el spike, que solo agrupaba las filas válidas.
        private static FilaValida ValidarFila(int posicion, FilaDeMapeo fila, List<ErrorDeNegocio> errores)
        {
            if (fila == null)
            {
                errores.Add(CodigosDeError.VER_007.Crear("la fila " + posicion.ToString(CultureInfo.InvariantCulture) + " del mapeo es nula"));
                return null;
            }

            if (string.IsNullOrWhiteSpace(fila.Hoja))
            {
                errores.Add(CodigosDeError.VER_007.Crear("la celda " + Comillas(fila.Celda) + " de " + EtiquetaDelCampo(fila.Campo) + " no indica la hoja"));
                return null;
            }

            int columna, numeroDeFila;
            if (!TryParsearCeldaA1(fila.Celda, out columna, out numeroDeFila))
            {
                errores.Add(CodigosDeError.VER_007.Crear("la celda " + Comillas(fila.Celda) + " de la hoja " + Comillas(fila.Hoja) + " ("
                    + EtiquetaDelCampo(fila.Campo) + ") está mal escrita; se espera una referencia como B7, de A1 a XFD1048576"));
                return null;
            }

            // Lista blanca (BP-PP-062): una opción nueva del choice que el código aún no conoce se frena al publicar, con su propio texto.
            if (!Enum.IsDefined(typeof(CampoMapeable), fila.Campo))
            {
                errores.Add(CodigosDeError.VER_007.Crear("la celda " + Comillas(fila.Celda) + " de la hoja " + Comillas(fila.Hoja) + " usa el campo "
                    + ((int)fila.Campo).ToString(CultureInfo.InvariantCulture) + ", que no es una opción conocida del mapeo"));
                return null;
            }

            if (!EsCompatible(fila.Campo, fila.Tipo))
            {
                errores.Add(CodigosDeError.VER_007.Crear("la celda " + Comillas(fila.Celda) + " de la hoja " + Comillas(fila.Hoja) + " usa el tipo de mapeo "
                    + EtiquetaDelTipo(fila.Tipo) + ", que no admite el campo " + EtiquetaDelCampo(fila.Campo)));
                return null;
            }

            return new FilaValida { Fila = fila, Columna = columna, NumeroDeFila = numeroDeFila };
        }

        // La hoja se compara sin distinguir mayúsculas (Excel tampoco las distingue en el nombre de una hoja; el spike agrupaba por
        // ToUpperInvariant) pero SIN recortar: " FORMATO ARP ELECTRONICO" con su espacio inicial es otra hoja que sin él (spike 1, hallazgo 1).
        // La celda se compara ya interpretada, así "b7" y "B7" son la misma.
        private static void ValidarCeldasRepetidas(List<FilaValida> validas, List<ErrorDeNegocio> errores)
        {
            var grupos = validas.GroupBy(v => ClaveDeHoja(v.Fila.Hoja) + "!" + v.Columna.ToString(CultureInfo.InvariantCulture) + ","
                + v.NumeroDeFila.ToString(CultureInfo.InvariantCulture), StringComparer.Ordinal);

            foreach (var grupo in grupos)
            {
                if (grupo.Count() < 2) continue;
                FilaDeMapeo primera = grupo.First().Fila;
                errores.Add(CodigosDeError.VER_007.Crear("dos campos en la misma celda " + Comillas(primera.Celda) + " de la hoja " + Comillas(primera.Hoja) + ": "
                    + string.Join(" y ", grupo.Select(g => EtiquetaDelCampo(g.Fila.Campo)))));
            }
        }

        // Hoja sin distinguir mayúsculas y SIN recortar (b, d y j comparan igual).
        private static string ClaveDeHoja(string hoja)
        {
            return hoja.ToUpperInvariant();
        }

        // Reglas d a k de la lista cerrada (diseno/03 2.0.7 regla 2). Miran solo las filas de bloque que pasaron a y c. Las filas máximas
        // nulas no dan error acá (k): las atrapa la regla 4.
        private static void ValidarBloque(List<FilaValida> validas, List<ErrorDeNegocio> errores)
        {
            var delBloque = validas.Where(v => v.Fila.Tipo == TipoDeMapeo.ColumnaDeBloqueDeCuotas).ToList();

            // d. Con el bloque repartido, los rangos no tienen sentido: es el único error del bloque (igual que el spike).
            if (delBloque.Select(v => ClaveDeHoja(v.Fila.Hoja)).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                errores.Add(CodigosDeError.VER_007.Crear("el bloque de cuotas debe estar en una sola hoja"));
                return;
            }

            // e. Una columna mapeada más de una vez: un error por columna repetida.
            foreach (var grupo in delBloque.GroupBy(v => v.Fila.Campo))
            {
                if (grupo.Count() > 1) errores.Add(CodigosDeError.VER_007.Crear("la columna de bloque " + EtiquetaDelCampo(grupo.Key) + " está mapeada más de una vez"));
            }

            // f. Las cinco columnas, una vez cada una. Un solo error que nombra todas las que faltan (spike: "Bloque de cuotas incompleto").
            var presentes = new HashSet<CampoMapeable>(delBloque.Select(v => v.Fila.Campo));
            var faltantes = CamposDelBloque.Where(c => !presentes.Contains(c)).ToList();
            if (faltantes.Count > 0)
            {
                errores.Add(CodigosDeError.VER_007.Crear("el bloque de cuotas está incompleto, faltan las columnas "
                    + string.Join(", ", faltantes.Select(EtiquetaDelCampo))));
            }
            if (delBloque.Count == 0) return;

            // g. Todas las columnas empiezan en la misma fila.
            if (delBloque.Select(v => v.NumeroDeFila).Distinct().Count() > 1)
            {
                errores.Add(CodigosDeError.VER_007.Crear("las columnas del bloque de cuotas deben empezar en la misma fila"));
            }

            // h e i. Filas máximas entre 1 y 60, y el bloque no se sale de la hoja. Aritmética de 64 bits: nada desborda.
            foreach (var v in delBloque)
            {
                if (!v.Fila.FilasMaximas.HasValue) continue;   // k
                int filas = v.Fila.FilasMaximas.Value;
                if (filas < 1 || filas > GeneradorDeCalendario.PlazoMaximo)
                {
                    errores.Add(CodigosDeError.VER_007.Crear("las filas máximas del bloque en la celda " + Comillas(v.Fila.Celda) + " ("
                        + filas.ToString(CultureInfo.InvariantCulture) + ") deben estar entre 1 y "
                        + GeneradorDeCalendario.PlazoMaximo.ToString(CultureInfo.InvariantCulture)));
                }
                else if ((long)v.NumeroDeFila + filas - 1 > MaxFila)
                {
                    errores.Add(CodigosDeError.VER_007.Crear("el bloque de cuotas que empieza en la celda " + Comillas(v.Fila.Celda) + " se sale de la hoja"));
                }
            }

            // j. Otra celda mapeada (valor simple o total) de la misma hoja dentro del rango de una columna del bloque: un error por pareja.
            // Para el rango, filas máximas menores que 1 o nulas valen 1 y las mayores que 60 valen lo que traen (Math.Max(…, 1) del spike).
            foreach (var otra in validas.Where(v => v.Fila.Tipo != TipoDeMapeo.ColumnaDeBloqueDeCuotas))
            {
                foreach (var columna in delBloque)
                {
                    if (ClaveDeHoja(otra.Fila.Hoja) != ClaveDeHoja(columna.Fila.Hoja)) continue;
                    if (otra.Columna != columna.Columna) continue;

                    long filas = Math.Max(columna.Fila.FilasMaximas ?? 1, 1);
                    long ultima = columna.NumeroDeFila + filas - 1;
                    if (otra.NumeroDeFila >= columna.NumeroDeFila && otra.NumeroDeFila <= ultima)
                    {
                        errores.Add(CodigosDeError.VER_007.Crear("la celda " + Comillas(otra.Fila.Celda) + " (" + EtiquetaDelCampo(otra.Fila.Campo)
                            + ") se pisa con el bloque de cuotas (" + EtiquetaDelCampo(columna.Fila.Campo) + ", filas "
                            + columna.NumeroDeFila.ToString(CultureInfo.InvariantCulture) + " a " + ultima.ToString(CultureInfo.InvariantCulture) + ")"));
                    }
                }
            }
        }

        // ---- Regla 4 ----

        // UN error aunque varias columnas difieran: el texto de CARP-VER-008 no nombra la columna. Cuenta toda fila de tipo bloque, esté
        // bien o mal en lo demás, para que el 008 viaje junto a los otros errores ("devuelve todos").
        private static void ValidarCapacidad(int capacidad, IReadOnlyList<FilaDeMapeo> filasDeMapeo, List<ErrorDeNegocio> errores)
        {
            bool algunaDifiere = filasDeMapeo.Any(f => f != null && f.Tipo == TipoDeMapeo.ColumnaDeBloqueDeCuotas && f.FilasMaximas != capacidad);
            if (algunaDifiere) errores.Add(CodigosDeError.VER_008.Crear(capacidad));
        }

        // ---- Tabla de compatibilidad (dicc. §3.1, columna 4) ----

        // Lista blanca (BP-PP-062, dicc. §3.1 columna 4): lo que no está en una de las tres listas no es compatible con ningún tipo.
        private static readonly IReadOnlyList<CampoMapeable> CamposDelBloque = new ReadOnlyCollection<CampoMapeable>(new[]
        {
            CampoMapeable.CuotaNumero, CampoMapeable.CuotaFechaDeVencimiento, CampoMapeable.CuotaMonto,
            CampoMapeable.CuotaFechaDePago, CampoMapeable.CuotaMontoPagado
        });

        private static readonly IReadOnlyList<CampoMapeable> CamposDeTotal = new ReadOnlyCollection<CampoMapeable>(new[] { CampoMapeable.TotalMontosDeCuota, CampoMapeable.TotalMontosPagados });

        private static readonly IReadOnlyList<CampoMapeable> CamposSimples = new ReadOnlyCollection<CampoMapeable>(new[]
        {
            CampoMapeable.FechaDeFormalizacion, CampoMapeable.OficialDeArreglo, CampoMapeable.NombreDelCliente, CampoMapeable.CedulaDelCliente,
            CampoMapeable.CorreoDelCliente, CampoMapeable.CelularDelCliente, CampoMapeable.NumeroDePrestamoOTarjeta, CampoMapeable.TipoDeArreglo,
            CampoMapeable.NumeroDeArreglo, CampoMapeable.SaldoInicial, CampoMapeable.MontoAbonado, CampoMapeable.SaldoRemanente,
            CampoMapeable.PlazoEnCuotas, CampoMapeable.CuotaMensual, CampoMapeable.TotalAPagar, CampoMapeable.FechaDeInicio,
            CampoMapeable.FechaDeFinalizacion, CampoMapeable.CuentaADebitar, CampoMapeable.Comentarios, CampoMapeable.Moneda
        });

        internal static bool EsCompatible(CampoMapeable campo, TipoDeMapeo tipo)
        {
            switch (tipo)
            {
                case TipoDeMapeo.ValorSimple: return CamposSimples.Contains(campo);
                case TipoDeMapeo.ColumnaDeBloqueDeCuotas: return CamposDelBloque.Contains(campo);
                case TipoDeMapeo.TotalCalculado: return CamposDeTotal.Contains(campo);
                default: return false;
            }
        }

        // ---- Celda en notación A1 ----

        private const int MaxColumna = 16384;   // XFD
        private const int MaxFila = 1048576;

        // Solo LETRAS ASCII + NÚMERO canónicos: sin espacios, sin $, sin rangos, sin ceros a la izquierda (portado de ReferenciaCelda.TryParse
        // del spike 1). La celda viene de una fila editable por personas: se interpreta, nunca se asume.
        internal static bool TryParsearCeldaA1(string texto, out int columna, out int fila)
        {
            columna = 0;
            fila = 0;
            if (string.IsNullOrEmpty(texto) || texto.Length > 10) return false;

            int i = 0;
            long col = 0;
            while (i < texto.Length && EsLetraAscii(texto[i]))
            {
                col = col * 26 + (char.ToUpperInvariant(texto[i]) - 'A' + 1);
                i++;
                if (i > 3) return false;
            }
            if (i == 0 || col > MaxColumna) return false;

            int inicioDeDigitos = i;
            if (i >= texto.Length || texto[i] == '0') return false;

            long numero = 0;
            while (i < texto.Length && texto[i] >= '0' && texto[i] <= '9')
            {
                numero = numero * 10 + (texto[i] - '0');
                i++;
            }
            if (i != texto.Length || i == inicioDeDigitos || numero > MaxFila) return false;

            columna = (int)col;
            fila = (int)numero;
            return true;
        }

        private static bool EsLetraAscii(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        // ---- Textos ----

        private static string Comillas(string texto)
        {
            return texto == null ? "(vacía)" : "'" + texto + "'";
        }

        private static string EtiquetaDelTipo(TipoDeMapeo tipo)
        {
            switch (tipo)
            {
                case TipoDeMapeo.ValorSimple: return "valor simple";
                case TipoDeMapeo.TotalCalculado: return "total calculado";
                case TipoDeMapeo.ColumnaDeBloqueDeCuotas: return "columna de bloque de cuotas";
                default: return "desconocido";
            }
        }

        // Etiqueta con tildes de cada opción de dicc. §3.1 (las del diccionario van sin tilde porque son nombres de componente).
        private static string EtiquetaDelCampo(CampoMapeable campo)
        {
            string etiqueta;
            return EtiquetasDeCampo.TryGetValue(campo, out etiqueta) ? "'" + etiqueta + "'" : "'" + campo.ToString() + "'";
        }

        internal static readonly IReadOnlyDictionary<CampoMapeable, string> EtiquetasDeCampo = new ReadOnlyDictionary<CampoMapeable, string>(new Dictionary<CampoMapeable, string>
        {
            { CampoMapeable.FechaDeFormalizacion, "Fecha de formalización" },
            { CampoMapeable.OficialDeArreglo, "Oficial de arreglo" },
            { CampoMapeable.NombreDelCliente, "Nombre del cliente" },
            { CampoMapeable.CedulaDelCliente, "Cédula del cliente" },
            { CampoMapeable.CorreoDelCliente, "Correo del cliente" },
            { CampoMapeable.CelularDelCliente, "Celular del cliente" },
            { CampoMapeable.NumeroDePrestamoOTarjeta, "Número de préstamo o tarjeta" },
            { CampoMapeable.TipoDeArreglo, "Tipo de arreglo" },
            { CampoMapeable.NumeroDeArreglo, "Número de arreglo" },
            { CampoMapeable.SaldoInicial, "Saldo inicial" },
            { CampoMapeable.MontoAbonado, "Monto abonado" },
            { CampoMapeable.SaldoRemanente, "Saldo remanente" },
            { CampoMapeable.PlazoEnCuotas, "Plazo en cuotas" },
            { CampoMapeable.CuotaMensual, "Cuota mensual" },
            { CampoMapeable.TotalAPagar, "Total a pagar" },
            { CampoMapeable.FechaDeInicio, "Fecha de inicio" },
            { CampoMapeable.FechaDeFinalizacion, "Fecha de finalización" },
            { CampoMapeable.CuentaADebitar, "Cuenta a debitar" },
            { CampoMapeable.Comentarios, "Comentarios" },
            { CampoMapeable.CuotaNumero, "Cuota: número" },
            { CampoMapeable.CuotaFechaDeVencimiento, "Cuota: fecha de vencimiento" },
            { CampoMapeable.CuotaMonto, "Cuota: monto" },
            { CampoMapeable.CuotaFechaDePago, "Cuota: fecha de pago" },
            { CampoMapeable.CuotaMontoPagado, "Cuota: monto pagado" },
            { CampoMapeable.TotalMontosDeCuota, "Total: montos de cuota" },
            { CampoMapeable.TotalMontosPagados, "Total: montos pagados" },
            { CampoMapeable.Moneda, "Moneda" }
        });
    }
}
