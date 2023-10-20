using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Toolkit.Uwp.Notifications;
using MySql.Data.MySqlClient;
using Serilog;

namespace GNS
{
    internal class Program
    {
        private static DateTime _lastNewDate;
        private static readonly ILogger Logger = new LoggerConfiguration()
            .WriteTo.File("log.txt")
            .CreateLogger();
        
        public static void Main(string[] args)
        {
            var executablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var projectPath = Path.GetDirectoryName(Path.GetDirectoryName(executablePath));
            var configuration = new ConfigurationBuilder()
                .SetBasePath(executablePath)
                .AddJsonFile("appsettings.json")
                .Build();
            
            Log.Logger = Logger;
            var connectionString = configuration["AppSettings:ConnectionString"];

            using (IDbConnection connection = new MySqlConnection(connectionString))
            {
                try
                {
                    Log.Information("Приложение стартанула...");

                    while (true)
                    {
                        CheckTable(connection);
                        Thread.Sleep(TimeSpan.FromSeconds(GetInterval(configuration)));
                    }
                }
                catch (MySqlException ex)
                {
                    Log.Error("Ошибка: {ErrorMessage}", ex.Message);
                }
                catch (Exception ex)
                {
                    Log.Error("Ошибка: {ErrorMessage}", ex.Message);
                }
            }
        }

        static void ShowNotification(string title, string body)
        {
            new ToastContentBuilder()
                .AddText("Новый отзыв\n" + title, AdaptiveTextStyle.Header)
                .AddText(body, AdaptiveTextStyle.Body)
                .Show();
        }
        
        static void CheckTable(IDbConnection connection)
        {
            const string query = @"SELECT CONCAT(c.service_prefix, c.number) as 'talon', re.resp_date, u.name as 'userName', r.name as 'review', re.comment 
                            FROM response_event re
                            LEFT JOIN responses r ON r.id = re.response_id
                            LEFT JOIN users u ON u.id = re.users_id
                            LEFT JOIN clients c ON c.id = re.clients_id
                            ORDER BY re.resp_date DESC";
            
            var result = connection.QueryFirstOrDefault<dynamic>(query);

            if (result.resp_date > _lastNewDate)
            {
                _lastNewDate = result.resp_date;
                
                ShowNotification($"{result.talon} - {result.userName}", $"{result.review}: {result.comment}");
            }
        }

        static int GetInterval(IConfigurationRoot configuration)
        {
            return configuration.GetSection("AppSettings")["IntervalInSeconds"] != null
                ? int.Parse(configuration["AppSettings:IntervalInSeconds"])
                : 30; // Default value
        }
    }
}