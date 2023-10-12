using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading;
using Windows.UI.Notifications;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace WindowsReviewNotification
{
    internal class Program
    {
        public static DateTime LastNewDate;
        
        public static void Main(string[] args)
        {
            // Get the path to the directory where the executable is located.
            var executablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var projectPath = Path.GetDirectoryName(Path.GetDirectoryName(executablePath));
            
            var configuration = new ConfigurationBuilder()
                .SetBasePath(projectPath)
                .AddJsonFile("appsettings.json")
                .Build();

            var intervalInSeconds = configuration.GetSection("AppSettings")["IntervalInSeconds"] != null
                ? int.Parse(configuration["AppSettings:IntervalInSeconds"])
                : 30; // Default value

            var connectionString = configuration["AppSettings:ConnectionString"];

            using (IDbConnection connection = new MySqlConnection(connectionString))
            {
                try
                {
                    while (true)
                    {
                        CheckTable(connection);
                        Thread.Sleep(TimeSpan.FromSeconds(intervalInSeconds));
                    }
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }

        static void ShowNotification(string title, string body)
        {
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

            // Set the title and body of the toast notification
            var textElements = toastXml.GetElementsByTagName("text");
            textElements[0].InnerText = title;
            textElements[1].InnerText = body;

            var toast = new ToastNotification(toastXml)
            {
                ExpirationTime = DateTimeOffset.Now.AddMinutes(5)
            };

            var notifier = ToastNotificationManager.CreateToastNotifier("\t   Новый отзыв");
            notifier.Show(toast);
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

            if (result.resp_date > LastNewDate)
            {
                LastNewDate = result.resp_date;
                
                ShowNotification($"{result.talon} - {result.userName}", $"{result.review}: {result.comment}");
            }
        }
    }
}