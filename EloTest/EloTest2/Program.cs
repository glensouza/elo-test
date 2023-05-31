using EloTest;

// Create a list of pictures
List<Picture> pictures = new()
{
    new Picture { Name = "Mona Lisa" },
    new Picture { Name = "The Scream" },
    new Picture { Name = "The Starry Night" },
    new Picture { Name = "The Persistence of Memory" },
    new Picture { Name = "The Last Supper" }
};

int kFactor = 32;

// Rate each picture against all other pictures
for (int i = 0; i < pictures.Count; i++)
{
    Picture picture1 = pictures[i];
    for (int j = 0; j < pictures.Count; j++)
    {
        if (i == j) continue;
        Picture picture2 = pictures[j];

        // Calculate the expected scores for each picture
        double expectedScore1 = 1 / (1 + Math.Pow(10, (picture2.Rating - picture1.Rating) / 400));
        double expectedScore2 = 1 / (1 + Math.Pow(10, (picture1.Rating - picture2.Rating) / 400));

        // Ask the user to select the better picture
        Console.WriteLine($"Which picture is better: {picture1.Name} or {picture2.Name}?");
        Console.WriteLine("Enter 1 for the first picture or 2 for the second picture:");
        int userSelection = int.Parse(Console.ReadLine() ?? "0");
        double score1 = userSelection == 1 ? 1 : 0;
        double score2 = userSelection == 2 ? 1 : 0;

        // Update the ratings for each picture
        picture1.Rating += kFactor * (score1 - expectedScore1);
        picture2.Rating += kFactor * (score2 - expectedScore2);
    }
}

// Display the final ratings for all pictures
Console.WriteLine("Final ratings for all pictures:");
foreach (Picture picture in pictures)
{
    Console.WriteLine($"{picture.Name}: {picture.Rating}");
}

/*
// Rate each picture against all other pictures in a random order
Random rnd = new();
while (pictures.Count > 1)
{
    // Select two random pictures from the list
    int index1 = rnd.Next(pictures.Count);
    int index2 = rnd.Next(pictures.Count);
    if (index1 == index2) continue;
    Picture picture1 = pictures[index1];
    Picture picture2 = pictures[index2];

    // Calculate the expected scores for each picture
    double expectedScore1 = 1 / (1 + Math.Pow(10, (picture2.Rating - picture1.Rating) / 400));
    double expectedScore2 = 1 / (1 + Math.Pow(10, (picture1.Rating - picture2.Rating) / 400));

    // Ask the user to select the better picture
    Console.WriteLine($"Which picture is better: {picture1.Name} or {picture2.Name}?");
    Console.WriteLine("Enter 1 for the first picture or 2 for the second picture:");
    int userSelection = int.Parse(Console.ReadLine() ?? "1");
    double score1 = userSelection == 1 ? 1 : 0;
    double score2 = userSelection == 2 ? 1 : 0;

    // Update the ratings for each picture
    picture1.Rating += 32 * (score1 - expectedScore1);
    picture2.Rating += 32 * (score2 - expectedScore2);

    // Remove the rated pictures from the list
    pictures.Remove(picture1);
    pictures.Remove(picture2);
}

// Display the final rating for the remaining picture
Console.WriteLine($"The final rating for {pictures[0].Name} is {pictures[0].Rating}.");
*/
