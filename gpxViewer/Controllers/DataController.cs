using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Drawing;

public class DataController : Controller
{
    private readonly IWebHostEnvironment _hostingEnvironment;
    private static List<dynamic> points;

    public DataController(IWebHostEnvironment hostingEnvironment)
    {
        _hostingEnvironment = hostingEnvironment;
    }

    public IActionResult GetChartData()
    {
        var filePath = Path.Combine(_hostingEnvironment.ContentRootPath, "gpxfile.gpx");
        var gpxData = XDocument.Load(filePath);

        // 假设 GPX 文件包含标准的 trkpt 元素
        points = gpxData.Descendants("{http://www.topografix.com/GPX/1/1}trkpt")
                            .Select((pt, index) => new { Point = pt, Index = index })
                            .Where(x => x.Index % 10 == 0) // 每隔10个点取一个
                            .Select(x => new
                            {
                                Latitude = (double)x.Point.Attribute("lat"),
                                Longitude = (double)x.Point.Attribute("lon"),
                                Elevation = (double)x.Point.Element("{http://www.topografix.com/GPX/1/1}ele"),
                                Time =(string)x.Point.Element("{http://www.topografix.com/GPX/1/1}time")
                            }).Cast<dynamic>() // 将匿名类型转换为 dynamic
                            .ToList();

        // 根据需要进一步处理 points 数据
        // 例如计算距离、时间等，并生成 Chart.js 所需的数据格式

        return Json(points);
    }

    [HttpGet]
    public IActionResult GetGpxFile()
    {
        var filePath = Path.Combine(_hostingEnvironment.ContentRootPath, "gpxfile.gpx");
        var gpxContent = System.IO.File.ReadAllText(filePath);
        return Content(gpxContent, "text/plain");
    }

    [HttpPost]
    public IActionResult CalculateAscentAndDistance([FromBody] PointsData data)
    {

        if (data.Points == null || data.Points.Length < 2)
        {
            return BadRequest("Invalid data.");
        }

        // 假设数组中的两个整数是需要的数据
        int startIndex = data.Points[0];
        int endIndex = data.Points[1];

        double distance = CalculateDistance(startIndex, endIndex);

        // 在这里根据 point1 和 point2 计算总爬升和距离
        // 例如，这里只是简单地将两点相减
        double totalAscent, totalDescent;
        CalculateAscentAndDescent(startIndex, endIndex,2, out totalAscent, out totalDescent);

        // 创建一个对象来保存和返回计算结果
        var result = new
        {
            Ascent = totalAscent,
            Dscent = totalDescent,
            Distance = distance,
            Time = CalculateTotalTime(startIndex, endIndex)
        };

        return Ok(result);
    }

    public double CalculateDistance(int startIndex, int endIndex)
    {
        dynamic distance = 0;
        for (int i = startIndex; i < endIndex; i++)
        {
            distance += NewMethod(points[i], points[i+1]);
        }
         
        return distance;
    }

    private static dynamic NewMethod(dynamic point1, dynamic point2)
    {
        var R = 6371e3; // 地球半径，单位为米
        var lat1 = point1.Latitude * Math.PI / 180;
        var lat2 = point2.Latitude * Math.PI / 180;
        var deltaLat = (point2.Latitude - point1.Latitude) * Math.PI / 180;
        var deltaLon = (point2.Longitude - point1.Longitude) * Math.PI / 180;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        var distance = R * c; // 最终距离，单位为米
        return distance;
    }

    public void CalculateAscentAndDescent(int startIndex, int endIndex, int windowSize, out double totalAscent, out double totalDescent)
    {
        totalAscent = 0.0;
        totalDescent = 0.0;

        for (int i = startIndex; i < endIndex; i++)
        {
            var avgElevation1 = CalculateMovingAverage(i, windowSize);
            var avgElevation2 = CalculateMovingAverage(i + 1, windowSize);

            var elevationChange = avgElevation2 - avgElevation1;
            if (elevationChange > 0)
            {
                totalAscent += elevationChange;
            }
            else
            {
                totalDescent += Math.Abs(elevationChange);
            }
        }
    }

    public TimeSpan CalculateTotalTime(int startIndex, int endIndex)
    {
        DateTime startTime = DateTime.Parse(points[startIndex].Time);
        DateTime endTime = DateTime.Parse(points[endIndex].Time);
        return endTime - startTime;
    }

    public double CalculateMovingAverage(int index, int windowSize)
    {
        double sum = 0.0;
        int actualWindowSize = 0;

        for (int i = Math.Max(0, index - windowSize); i <= Math.Min(points.Count - 1, index + windowSize); i++)
        {
            sum += points[i].Elevation;
            actualWindowSize++;
        }

        return actualWindowSize > 0 ? sum / actualWindowSize : 0.0;
    }


}

public class PointsData
{
    public int[] Points { get; set; }
}
