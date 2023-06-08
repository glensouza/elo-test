using System;
using System.Collections.Generic;
using System.IO;

namespace Api.Helpers;

public class CarNameGenerator
{
    private readonly List<string> carNames = new();

    public CarNameGenerator()
    {
        IEnumerable<string> lines = File.ReadLines("carNames.txt");
        foreach (string line in lines)
        {
            carNames.Add(line);
        }
    }

    public string GetRandomCarName()
    {
        carNames.Shuffle();
        int firstName = Random.Shared.Next(carNames.Count);
        if (carNames[firstName].Contains(' '))
        {
            return carNames[firstName];
        }

        int secondName = Random.Shared.Next(carNames.Count);
        if (carNames[secondName].Contains(' '))
        {
            return carNames[secondName];
        }

        string generatedCarName = $"{carNames[firstName]} {carNames[secondName]}";
        return generatedCarName;
    }
}
