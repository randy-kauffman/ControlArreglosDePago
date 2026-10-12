using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Errores
{
    /// <summary>
    /// Una fila de las tablas de errores de diseno/03 (codigo, texto con marcadores <c>{nombre}</c>, reintentable).
    /// Los marcadores se reemplazan por posicion, en el orden en que aparecen en el texto.
    /// </summary>
    public sealed class DefinicionDeError
    {
        public string Codigo { get; }

        /// <summary>Texto exacto del diseno, con los marcadores sin reemplazar.</summary>
        public string Texto { get; }

        /// <summary>Los <c>900</c> a <c>999</c> son transitorios: quien llama puede reintentar (diseno/03 0.5).</summary>
        public bool Reintentable { get; }

        /// <summary>Nombres de los marcadores sin llaves, en orden de aparicion y sin repetir.</summary>
        public IReadOnlyList<string> Marcadores { get; }

        public DefinicionDeError(string codigo, string texto)
        {
            Codigo = codigo;
            Texto = texto;
            Reintentable = int.Parse(codigo.Substring(codigo.Length - 3), System.Globalization.CultureInfo.InvariantCulture) >= 900;
            var marcadores = new List<string>();
            int i = 0;
            while (i < texto.Length)
            {
                int abre = texto.IndexOf('{', i);
                if (abre < 0) break;
                int cierra = texto.IndexOf('}', abre + 1);
                if (cierra < 0) break;
                string nombre = texto.Substring(abre + 1, cierra - abre - 1);
                if (!marcadores.Contains(nombre)) marcadores.Add(nombre);
                i = cierra + 1;
            }
            Marcadores = marcadores;
        }

        /// <summary>Arma el error reemplazando cada marcador, en orden, por el valor correspondiente (formato invariante).</summary>
        public ErrorDeNegocio Crear(params object[] valores)
        {
            int esperados = Marcadores.Count;
            int recibidos = valores == null ? 0 : valores.Length;
            if (esperados != recibidos)
            {
                throw new ArgumentException(Codigo + " espera " + esperados.ToString(System.Globalization.CultureInfo.InvariantCulture) + " valor(es) y recibio "
                    + recibidos.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".", nameof(valores));
            }
            string mensaje = Texto;
            for (int i = 0; i < esperados; i++)
            {
                object v = valores[i];
                string texto = v == null ? string.Empty : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
                mensaje = mensaje.Replace("{" + Marcadores[i] + "}", texto);
            }
            return new ErrorDeNegocio(Codigo, mensaje);
        }

        public override string ToString()
        {
            return Codigo + ": " + Texto;
        }
    }

}
