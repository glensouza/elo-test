using System;
using System.Collections.Generic;
using System.IO;

namespace Api;

public class CarNameGenerator
{
    private readonly List<string> carNames = new();

    public CarNameGenerator()
    {
        IEnumerable<string> lines = File.ReadLines("carNames.txt");
        foreach (string line in lines)
        {
            this.carNames.Add(line);
        }
    }

    public string GetRandomCarName()
    {
        this.carNames.Shuffle();
        int firstName = Random.Shared.Next(this.carNames.Count);
        int secondName = Random.Shared.Next(this.carNames.Count);
        return $"{this.carNames[firstName]} {this.carNames[secondName]}";
    }
}
