using System.Collections.ObjectModel;

namespace Dealership.Domain.Vehicles;

public static class VehicleTaxonomy
{
    // First model year each model was sold in Malaysia. A trim launched later overrides its model below.
    private static readonly Dictionary<string, int> modelFirstYears = new()
    {
        ["Perodua|Myvi"] = 2005,
        ["Perodua|Axia"] = 2014,
        ["Perodua|Ativa"] = 2021,
        ["Perodua|Bezza"] = 2016,
        ["Perodua|Alza"] = 2009,
        ["Proton|X50"] = 2020,
        ["Proton|Saga"] = 2008,
        ["Proton|X70"] = 2018,
        ["Proton|Persona"] = 2007,
        ["Proton|Iriz"] = 2014,
        ["Honda|Civic"] = 2016,
        ["Honda|City"] = 2014,
        ["Honda|HR-V"] = 2015,
        ["Honda|CR-V"] = 2017,
        ["Toyota|Vios"] = 2014,
        ["Toyota|Yaris"] = 2019,
        ["Toyota|Corolla Cross"] = 2020,
        ["Toyota|Hilux"] = 2016,
        ["Mazda|Mazda 2"] = 2015,
        ["Mazda|Mazda 3"] = 2014,
        ["Mazda|CX-5"] = 2013,
        ["BYD|Atto 3"] = 2022,
        ["BYD|Dolphin"] = 2024,
        ["BYD|Seal"] = 2024
    };

    private static readonly Dictionary<string, int> trimFirstYears = new()
    {
        ["Perodua|Myvi|1.5 AV"] = 2018,
        ["Perodua|Alza|AV"] = 2022,
        ["Proton|Saga|Premium S"] = 2019,
        ["Honda|Civic|e:HEV RS"] = 2022,
        ["Honda|City|RS"] = 2020,
        ["Honda|City|e:HEV RS"] = 2021,
        ["Honda|HR-V|RS"] = 2022,
        ["Honda|CR-V|e:HEV RS"] = 2023,
        ["Toyota|Corolla Cross|Hybrid"] = 2021,
        ["Mazda|Mazda 3|2.0 High Plus"] = 2019,
        ["Mazda|Mazda 3|2.0 Ignite Edition"] = 2020,
        ["Mazda|CX-5|2.2D"] = 2015,
        ["Mazda|CX-5|2.5 High"] = 2017,
        ["Mazda|CX-5|2.5 Turbo"] = 2020
    };

    public static IReadOnlyList<TaxonomyMake> Makes { get; } = Build();

    private static ReadOnlyCollection<TaxonomyMake> Build()
    {
        TaxonomyEntry[] entries =
        [
            new("Perodua", "Myvi", "1.3 G", 47000m),
            new("Perodua", "Myvi", "1.5 X", 51000m),
            new("Perodua", "Myvi", "1.5 H", 55000m),
            new("Perodua", "Myvi", "1.5 AV", 60000m),
            new("Perodua", "Axia", "G", 39000m),
            new("Perodua", "Axia", "SE", 44000m),
            new("Perodua", "Axia", "AV", 50000m),
            new("Perodua", "Ativa", "X", 63000m),
            new("Perodua", "Ativa", "H", 68000m),
            new("Perodua", "Ativa", "AV", 73000m),
            new("Perodua", "Bezza", "1.0 G", 35000m),
            new("Perodua", "Bezza", "1.3 X", 44000m),
            new("Perodua", "Bezza", "1.3 AV", 50000m),
            new("Perodua", "Alza", "X", 63000m),
            new("Perodua", "Alza", "H", 69000m),
            new("Perodua", "Alza", "AV", 76000m),
            new("Proton", "X50", "Standard", 86000m),
            new("Proton", "X50", "Executive", 93000m),
            new("Proton", "X50", "Premium", 102000m),
            new("Proton", "X50", "Flagship", 114000m),
            new("Proton", "Saga", "Standard", 35000m),
            new("Proton", "Saga", "Premium", 42000m),
            new("Proton", "Saga", "Premium S", 45000m),
            new("Proton", "X70", "Standard", 99000m),
            new("Proton", "X70", "Executive", 112000m),
            new("Proton", "X70", "Premium", 126000m),
            new("Proton", "Persona", "Standard", 48000m),
            new("Proton", "Persona", "Executive", 54000m),
            new("Proton", "Persona", "Premium", 60000m),
            new("Proton", "Iriz", "Standard", 43000m),
            new("Proton", "Iriz", "Executive", 50000m),
            new("Proton", "Iriz", "Active", 58000m),
            new("Honda", "Civic", "1.5 E", 134000m),
            new("Honda", "Civic", "1.5 V", 145000m),
            new("Honda", "Civic", "1.5 RS", 153000m),
            new("Honda", "Civic", "e:HEV RS", 168000m),
            new("Honda", "City", "S", 85000m),
            new("Honda", "City", "E", 90000m),
            new("Honda", "City", "V", 95000m),
            new("Honda", "City", "RS", 100000m),
            new("Honda", "City", "e:HEV RS", 112000m),
            new("Honda", "HR-V", "S", 116000m),
            new("Honda", "HR-V", "E", 131000m),
            new("Honda", "HR-V", "V", 136000m),
            new("Honda", "HR-V", "RS", 143000m),
            new("Honda", "CR-V", "1.5 Turbo S", 160000m),
            new("Honda", "CR-V", "1.5 Turbo E", 170000m),
            new("Honda", "CR-V", "1.5 Turbo V", 182000m),
            new("Honda", "CR-V", "e:HEV RS", 196000m),
            new("Toyota", "Vios", "1.5 E", 90000m),
            new("Toyota", "Vios", "1.5 G", 96000m),
            new("Toyota", "Yaris", "1.5 E", 89000m),
            new("Toyota", "Yaris", "1.5 G", 93000m),
            new("Toyota", "Corolla Cross", "1.8 G", 131000m),
            new("Toyota", "Corolla Cross", "1.8 V", 137000m),
            new("Toyota", "Corolla Cross", "Hybrid", 144000m),
            new("Toyota", "Hilux", "Double Cab 2.4 E", 115000m),
            new("Toyota", "Hilux", "2.4 V", 145000m),
            new("Toyota", "Hilux", "2.8 Rogue", 159000m),
            new("Mazda", "Mazda 2", "Hatchback", 109000m),
            new("Mazda", "Mazda 2", "Sedan", 109000m),
            new("Mazda", "Mazda 3", "2.0 High", 157000m),
            new("Mazda", "Mazda 3", "2.0 High Plus", 167000m),
            new("Mazda", "Mazda 3", "2.0 Ignite Edition", 177000m),
            new("Mazda", "CX-5", "2.0 Mid", 145000m),
            new("Mazda", "CX-5", "2.0 High", 163000m),
            new("Mazda", "CX-5", "2.5 High", 176000m),
            new("Mazda", "CX-5", "2.2D", 182000m),
            new("Mazda", "CX-5", "2.5 Turbo", 190000m),
            new("BYD", "Atto 3", "Standard Range", 130000m),
            new("BYD", "Atto 3", "Extended Range", 150000m),
            new("BYD", "Dolphin", "Dynamic Standard", 100000m),
            new("BYD", "Dolphin", "Premium Extended", 125000m),
            new("BYD", "Seal", "RWD Premium", 180000m),
            new("BYD", "Seal", "AWD Performance", 200000m),
        ];

        var makes = entries.GroupBy(entry => entry.Make).Select(make =>
        {
            var models = make.GroupBy(entry => entry.Model).Select(model =>
            {
                var trims = model.Select(entry => new TaxonomyTrim(entry.Trim, entry.BaselinePrice,
                    FirstYear(entry), null)).ToArray();
                return new TaxonomyModel(model.Key, Array.AsReadOnly(trims));
            }).ToArray();

            return new TaxonomyMake(make.Key, Array.AsReadOnly(models));
        }).ToArray();

        return Array.AsReadOnly(makes);
    }

    private static int FirstYear(TaxonomyEntry entry)
    {
        return trimFirstYears.TryGetValue($"{entry.Make}|{entry.Model}|{entry.Trim}", out var trimYear)
            ? trimYear
            : modelFirstYears[$"{entry.Make}|{entry.Model}"];
    }

    private sealed record TaxonomyEntry(string Make, string Model, string Trim, decimal BaselinePrice);
}

public sealed record TaxonomyMake(string Name, IReadOnlyList<TaxonomyModel> Models);

public sealed record TaxonomyModel(string Name, IReadOnlyList<TaxonomyTrim> Trims);

// LastYear is null while the trim is still sold, so the newest accepted year is the current Malaysian year.
public sealed record TaxonomyTrim(string Name, decimal BaselinePrice, int FirstYear, int? LastYear)
{
    public bool AcceptsYear(int year, int currentYear) => year >= FirstYear && year <= Math.Min(LastYear ?? currentYear, currentYear);
}
