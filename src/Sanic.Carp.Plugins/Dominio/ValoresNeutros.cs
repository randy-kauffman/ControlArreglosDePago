using System;

namespace Sanic.Carp.Plugins.Dominio
{
    // Tipos neutros que representan, dentro del dominio, los valores del SDK de Dataverse (diseno/03 4.4: "El dominio no usa Entity").
    // Los steps convierten Entity <-> IReadOnlyDictionary<string, object> en Infraestructura (ConversorDeEntidades):
    //   OptionSetValue -> ValorDeOpcion, EntityReference -> ReferenciaDeEntidad, Money -> ValorDeDinero.
    // El resto de los tipos viaja tal cual: null, bool, int, decimal, DateTime, Guid, string. Son datos sin lógica de comparación:
    // quien decide cuándo dos valores son "el mismo" es Cambios.

    /// <summary>Equivale a <c>OptionSetValue</c> (choices, <c>statecode</c>, <c>statuscode</c>).</summary>
    public sealed class ValorDeOpcion
    {
        public int Valor { get; }

        public ValorDeOpcion(int valor)
        {
            Valor = valor;
        }
    }

    /// <summary>Equivale a <c>EntityReference</c> (lookups, <c>ownerid</c>).</summary>
    public sealed class ReferenciaDeEntidad
    {
        public string Tabla { get; }
        public Guid Id { get; }

        public ReferenciaDeEntidad(string tabla, Guid id)
        {
            Tabla = tabla;
            Id = id;
        }
    }

    /// <summary>Equivale a <c>Money</c>.</summary>
    public sealed class ValorDeDinero
    {
        public decimal Valor { get; }

        public ValorDeDinero(decimal valor)
        {
            Valor = valor;
        }
    }
}
