using CarChecker.Server.Data;
using CarChecker.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CarChecker.Server
{
    public class SeedData
    {
        private const int NumVehicles = 1000;
        private static Random Random = new Random();

        public static void Initialize(ApplicationDbContext db)
        {
            db.Vehicles.AddRange(CreateSeedData());
            db.SaveChanges();
        }

        private static IEnumerable<Vehicle> CreateSeedData()
        {
            string[] makes = new[] { "Toyota", "Honda", "Mercedes", "Tesla", "BMW", "Kia", "Opel", "Mitsubishi", "Subaru", "Mazda", "Skoda", "Volkswagen", "Audi", "Chrysler", "Daewoo", "Peugeot", "Renault", "Seat", "Volvo", "Land Rover", "Porsche" };
            string[] models = new[] { "Sprint", "Fury", "Explorer", "Discovery", "305", "920", "Brightside", "XS", "Traveller", "Wanderer", "Pace", "Espresso", "Expert", "Jupiter", "Neptune", "Prowler" };

            for (int i = 0; i < NumVehicles; i++)
            {
                yield return new Vehicle
                {
                    LicenseNumber = GenerateRandomLicenseNumber(),
                    Make = PickRandom(makes),
                    Model = PickRandom(models),
                    RegistrationDate = new DateTime(PickRandomRange(2016, 2021), PickRandomRange(1, 13), PickRandomRange(1, 29)),
                    LastUpdated = DateTime.Now,
                    Mileage = PickRandomRange(500, 50000),
                    Tank = PickRandomEnum<FuelLevel>(),
                    Notes = Enumerable.Range(0, PickRandomRange(0, 5)).Select(_ => new InspectionNote
                    {
                        Location = PickRandomEnum<VehiclePart>(),
                        Text = GenerateRandomNoteText()
                    }).ToList()
                };
            }
        }

        private static string[] Adjectives = new[] { "Light", "Heavy", "Deep", "Long", "Short", "Substantial", "Slight", "Severe", "Problematic" };
        private static string[] Damages = new[] { "Scratch", "Dent", "Ding", "Break", "Discoloration" };
        private static string[] Relations = new[] { "towards", "behind", "near", "beside", "along" };
        private static string[] Positions = new[] { "Edge", "Side", "Top", "Back", "Front", "Inside", "Outside" };

        private static string GenerateRandomNoteText()
        {
            return PickRandom(new[]
            {
                $"{PickRandom(Adjectives)} {PickRandom(Damages).ToLower()}",
                $"{PickRandom(Adjectives)} {PickRandom(Damages).ToLower()} {PickRandom(Relations)} {PickRandom(Positions).ToLower()}",
                $"{PickRandom(Positions)} has {PickRandom(Damages).ToLower()}",
                $"{PickRandom(Positions)} has {PickRandom(Adjectives).ToLower()} {PickRandom(Damages).ToLower()}",
            });
        }

        private static int PickRandomRange(int minInc, int maxExc)
        {
            return Random.Next(minInc, maxExc);
        }

        private static T PickRandom<T>(T[] values)
        {
            return values[Random.Next(values.Length)];
        }

        public static T PickRandomEnum<T>()
        {
            return PickRandom((T[])Enum.GetValues(typeof(T)));
        }

        private static string GenerateRandomLicenseNumber()
        {
            StringBuilder result = new StringBuilder();
            result.Append(Random.Next(10));
            result.Append(Random.Next(10));
            result.Append(Random.Next(10));
            result.Append("-");
            result.Append((char)Random.Next('A', 'Z' + 1));
            result.Append((char)Random.Next('A', 'Z' + 1));
            result.Append((char)Random.Next('A', 'Z' + 1));
            return result.ToString();
        }
    }
}
