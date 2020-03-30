# qcells-aio-reader
Small .net core application to read out qcells battery values and store them in influxdb.
Run the appliation each time a data point should be retrieved and saved in influxdb. The retention is 365 days for the single data points.
Influxdb continuous queries do not support a relative time period for "last month". Therefore this application is checking frequently if it already stored aggregated values for the last month. If the value does not exist yet, it will be created.
The aggregation values are stored in a separate influx database with a longer retention period.
