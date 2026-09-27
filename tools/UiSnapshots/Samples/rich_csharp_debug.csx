using System;
using System.Collections.Generic;

var user = new
{
    Id = 10,
    Name = "Prashant",
    Address = new
    {
        City = "Patna",
        State = "Bihar"
    },
    Roles = new[] { "Admin", "Developer" }
};

var scores = new List<int> { 98, 95, 100 };
var matrix = new int[,] { { 1, 2 }, { 3, 4 } };
double gpa = 3.95;

Console.WriteLine($"Student {user.Name} has GPA {gpa}");
