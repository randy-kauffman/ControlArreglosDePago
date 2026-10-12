using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        /// Devuelve TODOS los errores, no solo el primero; lista vacía si el mapeo se puede publicar.
        /// <list type="number">
        /// <item>Mapeo nulo o vacío ⇒ <c>CARP-VER-007</c>.</item>
        /// <item>Reglas del generador (<c>ValidarMapeo</c> del spike 1): celda A1 bien formada (A1 a XFD1048576), sin dos campos en la
        /// misma celda de la misma hoja, bloque de cuotas con las cinco columnas, campo coherente con el tipo ⇒ <c>CARP-VER-007</c>,
        /// un error por problema.</item>
        /// <item>Tabla de compatibilidad de dicc. §3.1 (columna 4): las 19 primeras opciones y <c>Moneda</c> solo con ValorSimple;
        /// <c>Cuota: …</c> (5) solo con ColumnaDeBloqueDeCuotas; <c>Total: …</c> (2) solo con TotalCalculado ⇒ <c>CARP-VER-007</c>.</item>
        /// <item>Las filas máximas de las columnas del bloque no coinciden con <paramref name="capacidad"/> ⇒ UN solo error
        /// <c>CARP-VER-008</c> (decisión del arquitecto: el texto no nombra la columna).</item>
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
            ValidarBloqueCompleto(validas, errores);
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
            var grupos = validas.GroupBy(v => v.Fila.Hoja.ToUpperInvariant() + "!" + v.Columna.ToString(CultureInfo.InvariantCulture) + ","
                + v.NumeroDeFila.ToString(CultureInfo.InvariantCulture), StringComparer.Ordinal);

            foreach (var grupo in grupos)
            {
                if (grupo.Count() < 2) continue;
                FilaDeMapeo primera = grupo.First().Fila;
                errores.Add(CodigosDeError.VER_007.Crear("dos campos en la misma celda " + Comillas(primera.Celda) + " de la hoja " + Comillas(primera.Hoja) + ": "
                    + string.Join(" y ", grupo.Select(g => EtiquetaDelCampo(g.Fila.Campo)))));
            }
        }

        // Las cinco columnas del bloque, una vez cada una. Un solo error que las nombra todas (spike: "Bloque de cuotas incompleto").
        private static void ValidarBloqueCompleto(List<FilaValida> validas, List<ErrorDeNegocio> errores)
        {
            var presentes = new HashSet<CampoMapeable>(validas.Where(v => v.Fila.Tipo == TipoDeMapeo.ColumnaDeBloqueDeCuotas).Select(v => v.Fila.Campo));
            var faltantes = CamposDelBloque.Where(c => !presentes.Contains(c)).ToList();
            if (faltantes.Count == 0) return;

            errores.Add(CodigosDeError.VER_007.Crear("el bloque de cuotas está incompleto, faltan las columnas "
                + string.Join(", ", faltantes.Select(EtiquetaDelCampo))));
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

        private static readonly CampoMapeable[] CamposDelBloque =
        {
            CampoMapeable.CuotaNumero, CampoMapeable.CuotaFechaDeVencimiento, CampoMapeable.CuotaMonto,
            CampoMapeable.CuotaFechaDePago, CampoMapeable.CuotaMontoPagado
        };

        private static readonly CampoMapeable[] CamposDeTotal = { CampoMapeable.TotalMontosDeCuota, CampoMapeable.TotalMontosPagados };

        private static bool EsCompatible(CampoMapeable campo, TipoDeMapeo tipo)
        {
            bool esDeBloque = Array.IndexOf(CamposDelBloque, campo) >= 0;
            bool esDeTotal = Array.IndexOf(CamposDeTotal, campo) >= 0;
            switch (tipo)
            {
                case TipoDeMapeo.ValorSimple: return !esDeBloque && !esDeTotal;
                case TipoDeMapeo.ColumnaDeBloqueDeCuotas: return esDeBloque;
                case TipoDeMapeo.TotalCalculado: return esDeTotal;
                default: return false;
            }
        }

        // ---- Celda en notación A1 ----

        private const int MaxColumna = 16384;   // XFD
        private const int MaxFila = 1048576;

        // Solo LETRAS ASCII + NÚMERO canónicos: sin espacios, sin $, sin rangos, sin ceros a la izquierda (portado de ReferenciaCelda.TryParse
        // del spike 1). La celda viene de una fila editable por personas: se interpreta, nunca se asume.
        private static bool TryParsearCeldaA1(string texto, out int columna, out int fila)
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

        private static readonly Dictionary<CampoMapeable, string> EtiquetasDeCampo = new Dictionary<CampoMapeable, string>
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
        };
    }
}
