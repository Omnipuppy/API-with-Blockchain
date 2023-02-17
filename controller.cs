using Microsoft.AspNetCore.Mvc;
using Npgsql;

public class ValueController : Controller
{
    private readonly string connectionString = "Server=myServerAddress;Database=myDataBase;Username=myUsername;Password=myPassword;";

    public IActionResult Index()
    {
        var values = new List<Value>();

        using (NpgsqlConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=postgres;Database=mydatabase"))
        {
            connection.Open();

            using (NpgsqlCommand command = new NpgsqlCommand("SELECT * FROM values", connection))
            {
                NpgsqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    values.Add(new Value
                    {
                        Id = reader.GetInt32(0),
                        Model = reader.GetString(1),
                        Version = reader.GetString(2),
                        Modifier = reader.GetString(3),
                        Description = reader.GetString(4)
                    });
                }
            }
        }

        return View(values);
    }
}
