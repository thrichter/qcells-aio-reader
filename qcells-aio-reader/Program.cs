using System;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Text.Json;
using System.Net;
using AdysTech.InfluxDB.Client.Net;
using Microsoft.Extensions.Configuration;
using FluentDateTime;
using System.Collections.Generic;
using System.Threading;

namespace qcells_aio_reader
{
    class Program
    {

        private static InfluxDBClient influxClient;
        private static string influxDbName;
        private static string influxDbNameLongterm;
        private static string aio_url;
        private static string ret_policy_point = "pv-single-point";
        private static string ret_policy_longterm = "pvRetPolicyLongterm";
        private static string measurement_points = "AIO";
        private static string measurement_PVmonthly = "PVmonthly";
        

        public static void Main(string[] args)
        {
            while (true)
            {
                try
                {
                    var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();

                    influxClient = new InfluxDBClient(config["influx_url"], config["influx_admin"], config["influx_admin_pw"]);
                    influxDbName = config["influx_db_name"];
                    influxDbNameLongterm = config["influx_db_name_longterm"];
                    aio_url = config["aio_url"];
                  
                    WriteDataToInfluxDb().Wait();
                    Thread.Sleep(60000); //wait a minute
                }
                catch (Exception ex)
                {
                    Console.Write(ex.Message);
                }

            }
        }

        private static async Task<AIO_Values> FetchValuesFromAIO()
        {

            AIO_Values values = new AIO_Values();
            var htmlWeb = new HtmlWeb();
            var doc = await htmlWeb.LoadFromWebAsync(aio_url);

            // Using LINQ to get HTML table  
            var HTMLTableTRList = from table in doc.DocumentNode.SelectNodes("//table").Cast<HtmlNode>()
                                  from row in table.SelectNodes("//tr").Cast<HtmlNode>()
                                  from cell in row.SelectNodes("th|td").Cast<HtmlNode>()
                                  select new { Table_Name = table.Id, Cell_Text = cell.InnerText };

            // now parsing output from tabel
            int i = 0;
            foreach (var cell in HTMLTableTRList)

            {
                if (cell.Cell_Text == "BT_SOC")
                {
                    values.BatterySoc = float.Parse(HTMLTableTRList.ElementAt(i + 1).Cell_Text, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (cell.Cell_Text == "BT_P")
                {
                    values.BatteryP = float.Parse(HTMLTableTRList.ElementAt(i + 1).Cell_Text, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (cell.Cell_Text == "LOAD_P(30s)")
                {
                    values.Load = float.Parse(HTMLTableTRList.ElementAt(i + 1).Cell_Text, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (cell.Cell_Text == "PV_P(30s)")
                {
                    values.Pv = float.Parse(HTMLTableTRList.ElementAt(i + 1).Cell_Text, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (cell.Cell_Text == "GRID_P(30s)")
                {
                    values.Grid = float.Parse(HTMLTableTRList.ElementAt(i + 1).Cell_Text, System.Globalization.CultureInfo.InvariantCulture);
                    if (values.Grid > 0)
                    {
                        values.Demand = values.Grid;
                    }
                    else
                    {
                        values.FeedIn = values.Grid;
                    }
                }
                if (cell.Cell_Text == "INV_P(30s)")
                {
                    values.Inv = float.Parse(HTMLTableTRList.ElementAt(i + 1).Cell_Text, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (cell.Cell_Text == "Temp")
                {
                    values.Temp = float.Parse(HTMLTableTRList.ElementAt(i + 1).Cell_Text, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (cell.Cell_Text == "(ADC)")
                {
                    values.ADC = float.Parse(HTMLTableTRList.ElementAt(i + 1).Cell_Text, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                }
                i++;
            }
            return values;

        }

        private static InfluxDatapoint<InfluxValueField> CreateInfluxSeries(AIO_Values values)
        {
            var influxValues = new InfluxDatapoint<InfluxValueField>();
            influxValues.UtcTimestamp = DateTime.UtcNow;
            influxValues.Fields.Add("Pv", new InfluxValueField(values.Pv));
            influxValues.Fields.Add("Load", new InfluxValueField(values.Load));
            influxValues.Fields.Add("Inv", new InfluxValueField(values.Inv));
            influxValues.Fields.Add("Demand", new InfluxValueField(values.Demand));
            influxValues.Fields.Add("FeedIn", new InfluxValueField(values.FeedIn));
            influxValues.Fields.Add("Grid", new InfluxValueField(values.Grid));
            influxValues.Fields.Add("BatteryP", new InfluxValueField(values.BatteryP));
            influxValues.Fields.Add("BatterySoc", new InfluxValueField(values.BatterySoc));
            influxValues.Fields.Add("Temp", new InfluxValueField(values.Temp));
            influxValues.Fields.Add("ADC", new InfluxValueField(values.ADC));

            influxValues.MeasurementName = measurement_points;
            influxValues.Precision = TimePrecision.Seconds;
            influxValues.Retention = new InfluxRetentionPolicy() { Name = ret_policy_point };
            return influxValues;
        }

        private static async Task CreateRetentionPolicy()
        {
            var policies = await influxClient.GetRetentionPoliciesAsync(influxDbName);

            if (policies == null || !policies.Any(p => p.Name == ret_policy_point))
            {
                var rp_point = new InfluxRetentionPolicy() { Name = ret_policy_point, DBName = influxDbName, Duration = TimeSpan.FromDays(365), IsDefault = true };
                if (!await influxClient.CreateRetentionPolicyAsync(rp_point))
                    throw new InvalidOperationException("Unable to create Retention Policy for pint");
            }

        }

        private static async Task CreateRetentionPolicyLongterm()
        {
            var policies = await influxClient.GetRetentionPoliciesAsync(influxDbNameLongterm);

            if (policies == null || !policies.Any(p => p.Name == ret_policy_longterm))
            {
                var rp_month = new InfluxRetentionPolicy() { Name = ret_policy_longterm, DBName = influxDbNameLongterm, Duration = TimeSpan.FromDays(10950), IsDefault = true };
                if (!await influxClient.CreateRetentionPolicyAsync(rp_month))
                    throw new InvalidOperationException("Unable to create Retention Policy for month");
            }

        }

        private static async Task<MonthlyValues> GetCurrentMonthAggregation()
        {
            if (DateTime.Now.Hour == 01 && DateTime.Now.Minute == 00)  //we make this check only at 01:00 at night once a day
            {
                //get first and last day of last month
                string firstDayOfLastMonth = DateTime.Now.PreviousMonth().FirstDayOfMonth().BeginningOfDay().ToString("s") + "Z";
                string lastDayOfLastMonth = DateTime.Now.PreviousMonth().LastDayOfMonth().EndOfDay().ToString("s") + "Z";

                string monthIndex = DateTime.Now.PreviousMonth().FirstDayOfMonth().ToString("MM");
                string yearIndex = DateTime.Now.PreviousMonth().FirstDayOfMonth().ToString("yyyy");

                List<IInfluxSeries> resultsLastMonth = null;
                try
                {

                    resultsLastMonth = await influxClient.QueryMultiSeriesAsync(influxDbNameLongterm, $"SELECT * from {measurement_PVmonthly} WHERE YearIndex = '{yearIndex}' AND MonthIndex ='{monthIndex}' limit 1");
                }
                catch (InfluxDBException ex)
                {
                    Console.Write(ex.Message);
                }

                //If e have not craeted the result for last month, we write it now.
                if (resultsLastMonth == null || !resultsLastMonth.Any())
                {

                    //get aggregated values for the last month
                    List<IInfluxSeries> aggregatedLastMonth = null;
                    MonthlyValues monthlyValues = null;
                    try
                    {
                        aggregatedLastMonth = await influxClient.QueryMultiSeriesAsync(influxDbName, $"SELECT INTEGRAL(Pv,1h)/1000 as Pv_month,INTEGRAL(Load,1h)/1000 as Load_month, INTEGRAL(Inv,1h)/1000 as Inv_month,  INTEGRAL(Demand,1h)/1000 as Demand_month, INTEGRAL(FeedIn,1h)/1000 as FeedIn_month  FROM {measurement_points} WHERE time > '{firstDayOfLastMonth}' AND time < '{lastDayOfLastMonth}'");
                        monthlyValues = new MonthlyValues();
                        monthlyValues.PvMonth = float.Parse(aggregatedLastMonth.FirstOrDefault()?.Entries[0].Pv_month);
                        monthlyValues.LoadMonth = float.Parse(aggregatedLastMonth.FirstOrDefault()?.Entries[0].Load_month);
                        monthlyValues.InvMonth = float.Parse(aggregatedLastMonth.FirstOrDefault()?.Entries[0].Inv_month);
                        monthlyValues.DemandMonth = float.Parse(aggregatedLastMonth.FirstOrDefault()?.Entries[0].Demand_month);
                        monthlyValues.FeedInMonth = float.Parse(aggregatedLastMonth.FirstOrDefault()?.Entries[0].FeedIn_month);
                        monthlyValues.MonthIndex = monthIndex;
                        monthlyValues.YearIndex = yearIndex;
                    }
                    catch (InfluxDBException ex)
                    {
                        Console.Write(ex.Message);
                    }

                    return monthlyValues;
                }
            }
            return null;

        }

        private static async Task InsertMonthlyValues(MonthlyValues monthlyValues)
        {
            if (monthlyValues != null)
            {
                var influxMonthlyValues = new InfluxDatapoint<InfluxValueField>();
                influxMonthlyValues.UtcTimestamp = DateTime.Now.PreviousMonth().LastDayOfMonth().EndOfDay().ToUniversalTime();
                influxMonthlyValues.Fields.Add("PvMonth", new InfluxValueField(monthlyValues.PvMonth));
                influxMonthlyValues.Fields.Add("LoadMonth", new InfluxValueField(monthlyValues.LoadMonth));
                influxMonthlyValues.Fields.Add("InvMonth", new InfluxValueField(monthlyValues.InvMonth));
                influxMonthlyValues.Fields.Add("DemandMonth", new InfluxValueField(monthlyValues.DemandMonth));
                influxMonthlyValues.Fields.Add("FeedInMonth", new InfluxValueField(monthlyValues.FeedInMonth));
                influxMonthlyValues.Fields.Add("MonthIndex", new InfluxValueField(monthlyValues.MonthIndex));
                influxMonthlyValues.Fields.Add("YearIndex", new InfluxValueField(monthlyValues.YearIndex));

                influxMonthlyValues.MeasurementName = measurement_PVmonthly;
                influxMonthlyValues.Precision = TimePrecision.Minutes;
                influxMonthlyValues.Retention = new InfluxRetentionPolicy() { Name = ret_policy_longterm };
                var result = await influxClient.PostPointAsync(influxDbNameLongterm, influxMonthlyValues);  //save the monthly value
            }
        }

        private static async Task WriteDataToInfluxDb()
        {
            try
            {
                AIO_Values values = await FetchValuesFromAIO();
                //first the current data point
                bool success = await influxClient.CreateDatabaseAsync(influxDbName);  //create db if not exist
                if (success)
                {
                    await CreateRetentionPolicy();      // craete retention policy for minute data points if not exists
                    var point = CreateInfluxSeries(values);  //create the influx point
                    var result = await influxClient.PostPointAsync(influxDbName, point);  //save the data point
                    Console.Write("Wrote point to influx db");
                }
                //Now the longterm data
                success = await influxClient.CreateDatabaseAsync(influxDbNameLongterm);  //create longeterm db if not exist
                if (success)
                {
                    await CreateRetentionPolicyLongterm();
                    MonthlyValues monthlyValues = await GetCurrentMonthAggregation();  //check if we already have aggregations for the last month and year
                    await InsertMonthlyValues(monthlyValues);
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }
        }
    }
}
