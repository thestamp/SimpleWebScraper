using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

Console.Write("Enter the domain to scrape: ");
string domain = Console.ReadLine();
string outputFileName = "output.txt";

await ScrapeDomain(domain, outputFileName);

async Task ScrapeDomain(string domain, string outputFileName)
{
    var visitedUrls = new HashSet<string>();
    var urlsToVisit = new Queue<string>();
    urlsToVisit.Enqueue(domain);

    var config = Configuration.Default.WithDefaultLoader();
    var context = BrowsingContext.New(config);

    using var outputFile = File.CreateText(outputFileName);

    var processedLinks = new HashSet<string>();

    while (urlsToVisit.Count > 0)
    {
        string currentUrl = urlsToVisit.Dequeue();
        if (visitedUrls.Contains(currentUrl)) continue;
        visitedUrls.Add(currentUrl);

        try
        {
            IDocument document = await context.OpenAsync(currentUrl);

            outputFile.WriteLine($"URL: {currentUrl}");
            outputFile.WriteLine("Content:");

            foreach (var script in document.QuerySelectorAll("script"))
            {
                script.Remove();
            }

            var footer = document.QuerySelector("footer");
            if (footer != null)
            {
                footer.Remove();
            }

            var textNodes = document.QuerySelectorAll("p, span")
                .Where(node => node.QuerySelectorAll("a").Length == 0 && (!node.HasAttribute("class") || (!node.GetAttribute("class").Contains("aria-hide") && !node.GetAttribute("class").Contains("hidden"))))
                .ToList();

            var linkNodes = document.QuerySelectorAll("a")
                .OfType<IHtmlAnchorElement>()
                .Where(a => !processedLinks.Contains(a.TextContent.Trim()) && a.TextContent.Trim().Length > 0)
                .ToList();

            foreach (var link in linkNodes)
            {
                processedLinks.Add(link.TextContent.Trim());
                outputFile.WriteLine(link.TextContent.Trim());
            }

            foreach (var node in textNodes)
            {
                outputFile.WriteLine(node.TextContent.Trim());
            }

            outputFile.WriteLine("-----");

            var links = document.QuerySelectorAll("a[href]")
                .OfType<IHtmlAnchorElement>()
                .Select(a => a.Href)
                .Where(href => !string.IsNullOrWhiteSpace(href) && !href.StartsWith("#") && HasAllowedExtension(href) && !href.Contains("mailto:") && !href.Contains("tel:") && !href.StartsWith("javascript:"))
                .Select(href => ToAbsoluteUrl(currentUrl, href));

            foreach (string link in links)
            {
                if (!visitedUrls.Contains(link)) urlsToVisit.Enqueue(link);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {currentUrl}: {ex.Message}");
        }
    }
}

string ToAbsoluteUrl(string baseUrl, string relativeUrl)
{
    var baseUri = new Uri(baseUrl);
    var absoluteUri = new Uri(baseUri, relativeUrl);
    return absoluteUri.AbsoluteUri;
}

bool HasAllowedExtension(string url)
{
    string[] allowedExtensions = { ".html", ".htm", ".php", ".aspx" };
    string extension = Path.GetExtension(url).ToLower();
    return string.IsNullOrEmpty(extension) || allowedExtensions.Contains(extension);
}
