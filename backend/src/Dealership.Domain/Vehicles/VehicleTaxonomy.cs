using System.Collections.ObjectModel;

namespace Dealership.Domain.Vehicles;

public static class VehicleTaxonomy
{
    public static IReadOnlyList<TaxonomyMake> Makes { get; } = Build();

    private static ReadOnlyCollection<TaxonomyMake> Build()
    {
        TaxonomyEntry[] entries =
        [
            new("Perodua", "Myvi", "1.3 G", 47000m, 2005),
            new("Perodua", "Myvi", "1.5 X", 51000m, 2005),
            new("Perodua", "Myvi", "1.5 H", 55000m, 2005),
            new("Perodua", "Myvi", "1.5 AV", 60000m, 2018),
            new("Perodua", "Axia", "G", 39000m, 2014),
            new("Perodua", "Axia", "SE", 44000m, 2014),
            new("Perodua", "Axia", "AV", 50000m, 2014),
            new("Perodua", "Ativa", "X", 63000m, 2021),
            new("Perodua", "Ativa", "H", 68000m, 2021),
            new("Perodua", "Ativa", "AV", 73000m, 2021),
            new("Perodua", "Bezza", "1.0 G", 35000m, 2016),
            new("Perodua", "Bezza", "1.3 X", 44000m, 2016),
            new("Perodua", "Bezza", "1.3 AV", 50000m, 2016),
            new("Perodua", "Alza", "X", 63000m, 2009),
            new("Perodua", "Alza", "H", 69000m, 2009),
            new("Perodua", "Alza", "AV", 76000m, 2022),
            new("Proton", "X50", "Standard", 86000m, 2020),
            new("Proton", "X50", "Executive", 93000m, 2020),
            new("Proton", "X50", "Premium", 102000m, 2020),
            new("Proton", "X50", "Flagship", 114000m, 2020),
            new("Proton", "Saga", "Standard", 35000m, 2008),
            new("Proton", "Saga", "Premium", 42000m, 2008),
            new("Proton", "Saga", "Premium S", 45000m, 2019),
            new("Proton", "X70", "Standard", 99000m, 2018),
            new("Proton", "X70", "Executive", 112000m, 2018),
            new("Proton", "X70", "Premium", 126000m, 2018),
            new("Proton", "Persona", "Standard", 48000m, 2007),
            new("Proton", "Persona", "Executive", 54000m, 2007),
            new("Proton", "Persona", "Premium", 60000m, 2007),
            new("Proton", "Iriz", "Standard", 43000m, 2014),
            new("Proton", "Iriz", "Executive", 50000m, 2014),
            new("Proton", "Iriz", "Active", 58000m, 2014),
            new("Honda", "Civic", "1.5 E", 134000m, 2016),
            new("Honda", "Civic", "1.5 V", 145000m, 2016),
            new("Honda", "Civic", "1.5 RS", 153000m, 2016),
            new("Honda", "Civic", "e:HEV RS", 168000m, 2022),
            new("Honda", "City", "S", 85000m, 2014),
            new("Honda", "City", "E", 90000m, 2014),
            new("Honda", "City", "V", 95000m, 2014),
            new("Honda", "City", "RS", 100000m, 2020),
            new("Honda", "City", "e:HEV RS", 112000m, 2021),
            new("Honda", "HR-V", "S", 116000m, 2015),
            new("Honda", "HR-V", "E", 131000m, 2015),
            new("Honda", "HR-V", "V", 136000m, 2015),
            new("Honda", "HR-V", "RS", 143000m, 2022),
            new("Honda", "CR-V", "1.5 Turbo S", 160000m, 2017),
            new("Honda", "CR-V", "1.5 Turbo E", 170000m, 2017),
            new("Honda", "CR-V", "1.5 Turbo V", 182000m, 2017),
            new("Honda", "CR-V", "e:HEV RS", 196000m, 2023),
            new("Toyota", "Vios", "1.5 E", 90000m, 2014),
            new("Toyota", "Vios", "1.5 G", 96000m, 2014),
            new("Toyota", "Yaris", "1.5 E", 89000m, 2019),
            new("Toyota", "Yaris", "1.5 G", 93000m, 2019),
            new("Toyota", "Corolla Cross", "1.8 G", 131000m, 2020),
            new("Toyota", "Corolla Cross", "1.8 V", 137000m, 2020),
            new("Toyota", "Corolla Cross", "Hybrid", 144000m, 2021),
            new("Toyota", "Hilux", "Double Cab 2.4 E", 115000m, 2016),
            new("Toyota", "Hilux", "2.4 V", 145000m, 2016),
            new("Toyota", "Hilux", "2.8 Rogue", 159000m, 2016),
            new("Mazda", "Mazda 2", "Hatchback", 109000m, 2015),
            new("Mazda", "Mazda 2", "Sedan", 109000m, 2015),
            new("Mazda", "Mazda 3", "2.0 High", 157000m, 2014),
            new("Mazda", "Mazda 3", "2.0 High Plus", 167000m, 2019),
            new("Mazda", "Mazda 3", "2.0 Ignite Edition", 177000m, 2020),
            new("Mazda", "CX-5", "2.0 Mid", 145000m, 2013),
            new("Mazda", "CX-5", "2.0 High", 163000m, 2013),
            new("Mazda", "CX-5", "2.5 High", 176000m, 2017),
            new("Mazda", "CX-5", "2.2D", 182000m, 2015),
            new("Mazda", "CX-5", "2.5 Turbo", 190000m, 2020),
            new("BYD", "Atto 3", "Standard Range", 130000m, 2022),
            new("BYD", "Atto 3", "Extended Range", 150000m, 2022),
            new("BYD", "Dolphin", "Dynamic Standard", 100000m, 2023),
            new("BYD", "Dolphin", "Premium Extended", 125000m, 2023),
            new("BYD", "Seal", "RWD Premium", 180000m, 2024),
            new("BYD", "Seal", "AWD Performance", 200000m, 2024),
        ];

        var makes = entries.GroupBy(entry => entry.Make).Select(make =>
        {
            var models = make.GroupBy(entry => entry.Model).Select(model =>
            {
                var trims = model.Select(entry => new TaxonomyTrim(entry.Trim, entry.BaselinePrice,
                    entry.FirstYear)).ToArray();
                return new TaxonomyModel(model.Key, Array.AsReadOnly(trims));
            }).ToArray();

            return new TaxonomyMake(make.Key, Array.AsReadOnly(models));
        }).ToArray();

        return Array.AsReadOnly(makes);
    }

    // FirstYear is the first model year the trim was sold in Malaysia.
    private sealed record TaxonomyEntry(string Make, string Model, string Trim, decimal BaselinePrice, int FirstYear);
}

public sealed record TaxonomyMake(string Name, IReadOnlyList<TaxonomyModel> Models);

public sealed record TaxonomyModel(string Name, IReadOnlyList<TaxonomyTrim> Trims);

// The newest accepted year is the current Malaysian year.
public sealed record TaxonomyTrim(string Name, decimal BaselinePrice, int FirstYear)
{
    public bool AcceptsYear(int year, int currentYear) => year >= FirstYear && year <= currentYear;
}
