using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;

namespace Michaelhouse.Services
{
    // ============================================================
    // Supplier invoice scanning (Record Stock Delivery)
    //
    // Uses the project's existing Azure Document Intelligence set-up
    // (AzureDocIntelligence:Endpoint / Key, as AiReviewService does)
    // with Azure's prebuilt invoice model, and stores the file under
    // DocumentStorage:UploadRoot like other uploads.
    //
    // It ONLY reads the invoice. Matching (IngredientMatchingService),
    // the manager's confirmation and the stock update
    // (IngredientDeliveryService) all happen afterwards.
    // ============================================================

    public class InvoiceScanItem
    {
        public string Description { get; set; }
        public string ProductCode { get; set; }
        public decimal? Quantity { get; set; }
        public string Unit { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Amount { get; set; }
    }

    public class InvoiceScanResult
    {
        public InvoiceScanResult()
        {
            Items = new List<InvoiceScanItem>();
        }

        public bool Success { get; set; }
        public string Error { get; set; }

        public string VendorName { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string PurchaseOrderReference { get; set; }

        // All text on the invoice — used to find our PO number
        public string Content { get; set; }

        public List<InvoiceScanItem> Items { get; set; }

        // True when Azure's line items were unusable and the lines were
        // read from the table on the invoice instead
        public bool ItemsFromTable { get; set; }
    }

    public class InvoiceScanService
    {
        public const int MaxFileBytes = 10 * 1024 * 1024;
        private const string Folder = "cafeteria-invoices";

        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp" };

        private readonly string _endpoint;
        private readonly string _key;
        private readonly string _uploadRoot;

        public InvoiceScanService()
        {
            _endpoint = ConfigurationManager.AppSettings["AzureDocIntelligence:Endpoint"];
            _key = ConfigurationManager.AppSettings["AzureDocIntelligence:Key"];

            var relative = ConfigurationManager.AppSettings["DocumentStorage:UploadRoot"] ?? "~/App_Data/Uploads";
            _uploadRoot = relative.StartsWith("~")
                ? System.Web.Hosting.HostingEnvironment.MapPath(relative)
                : relative;
        }

        public bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(_endpoint) && !string.IsNullOrWhiteSpace(_key); }
        }

        // Saves the uploaded invoice; returns its path relative to the
        // upload root (e.g. cafeteria-invoices/2026/3f2a….pdf)
        public string Save(HttpPostedFileBase file, out string error)
        {
            error = null;

            if (file == null || file.ContentLength == 0)
            {
                error = "Choose a photo or PDF of the invoice.";
                return null;
            }

            if (file.ContentLength > MaxFileBytes)
            {
                error = "The invoice file is too large (10 MB maximum).";
                return null;
            }

            var ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                error = "Upload a PDF or a photo (JPG / PNG / TIFF) of the invoice.";
                return null;
            }

            if (string.IsNullOrEmpty(_uploadRoot))
            {
                error = "File storage isn't configured.";
                return null;
            }

            string relative = Folder + "/" + DateTime.UtcNow.Year + "/" + Guid.NewGuid().ToString("N") + ext;
            string full = FullPath(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            file.SaveAs(full);

            return relative;
        }

        // Stores a generated invoice (TestInvoiceService) exactly like an
        // upload, so viewing / scanning it uses the same paths and checks
        public string SaveGenerated(byte[] content, string extension)
        {
            var ext = (extension ?? "").ToLowerInvariant();
            if (content == null || content.Length == 0 || !AllowedExtensions.Contains(ext) || string.IsNullOrEmpty(_uploadRoot))
                return null;

            string relative = Folder + "/" + DateTime.UtcNow.Year + "/" + Guid.NewGuid().ToString("N") + ext;
            string full = FullPath(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, content);
            return relative;
        }

        // Absolute path for a stored invoice; null if the path isn't one
        // this service created
        public string FullPath(string relative)
        {
            if (!IsStoredInvoicePath(relative) || string.IsNullOrEmpty(_uploadRoot)) return null;
            return Path.Combine(_uploadRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        public static bool IsStoredInvoicePath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) return false;

            var parts = relative.Split('/');
            Guid g;
            int year;
            return parts.Length == 3
                && parts[0] == Folder
                && int.TryParse(parts[1], out year)
                && Guid.TryParseExact(Path.GetFileNameWithoutExtension(parts[2]), "N", out g)
                && AllowedExtensions.Contains(Path.GetExtension(parts[2]).ToLowerInvariant());
        }

        public InvoiceScanResult Analyse(string relativePath)
        {
            var result = new InvoiceScanResult();

            if (!IsConfigured)
            {
                result.Error = "Invoice scanning isn't set up (Azure Document Intelligence). Enter the delivery below.";
                return result;
            }

            var full = FullPath(relativePath);
            if (full == null || !File.Exists(full))
            {
                result.Error = "The invoice file could not be found.";
                return result;
            }

            try
            {
                var client = new DocumentAnalysisClient(new Uri(_endpoint), new AzureKeyCredential(_key));

                AnalyzeResult analysed;
                using (var stream = new FileStream(full, FileMode.Open, FileAccess.Read))
                {
                    analysed = client.AnalyzeDocument(WaitUntil.Completed, "prebuilt-invoice", stream).Value;
                }

                result.Content = analysed.Content;

                var doc = analysed.Documents.FirstOrDefault();
                if (doc == null)
                {
                    result.Error = "No invoice was found in the document.";
                    return result;
                }

                result.VendorName = Text(doc.Fields, "VendorName");
                result.InvoiceNumber = Text(doc.Fields, "InvoiceId");
                result.InvoiceDate = Date(doc.Fields, "InvoiceDate");
                result.PurchaseOrderReference = Text(doc.Fields, "PurchaseOrder");

                DocumentField items;
                if (doc.Fields.TryGetValue("Items", out items) && items.FieldType == DocumentFieldType.List)
                {
                    foreach (var item in items.Value.AsList())
                    {
                        if (item.FieldType != DocumentFieldType.Dictionary) continue;
                        var f = item.Value.AsDictionary();

                        result.Items.Add(new InvoiceScanItem
                        {
                            Description = Text(f, "Description") ?? item.Content,
                            ProductCode = Text(f, "ProductCode"),
                            Quantity = Number(f, "Quantity"),
                            Unit = Text(f, "Unit"),
                            UnitPrice = Money(f, "UnitPrice"),
                            Amount = Money(f, "Amount")
                        });
                    }
                }

                // Azure finds line items mainly by their amounts. On an
                // invoice without prices it can return empty items, so
                // drop those and read the invoice's table instead.
                result.Items = result.Items.Where(IsUsable).ToList();
                if (result.Items.Count == 0)
                {
                    foreach (var table in analysed.Tables)
                    {
                        var rows = table.Cells
                            .GroupBy(c => c.RowIndex)
                            .OrderBy(g => g.Key)
                            .Select(g => new TableRow
                            {
                                IsHeader = g.Any(c => c.Kind == DocumentTableCellKind.ColumnHeader),
                                Cells = Enumerable.Range(0, table.ColumnCount)
                                    .Select(i => g.Where(c => c.ColumnIndex == i).Select(c => c.Content).FirstOrDefault())
                                    .ToList()
                            })
                            .ToList();

                        var fromTable = ItemsFromTable(rows);
                        if (fromTable.Count > 0)
                        {
                            result.Items = fromTable;
                            result.ItemsFromTable = true;
                            break;
                        }
                    }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("Invoice scan failed: " + ex.Message);
                result.Error = "The invoice could not be read. Enter the delivery below.";
            }

            return result;
        }

        private static bool IsUsable(InvoiceScanItem item)
        {
            return !string.IsNullOrWhiteSpace(item.Description) && item.Quantity.HasValue;
        }

        // ── Table fallback ──

        public class TableRow
        {
            public bool IsHeader { get; set; }
            public List<string> Cells { get; set; }
        }

        private static readonly string[] DescriptionHeaders = { "description", "item", "items", "itemdescription", "product", "productdescription", "details", "goods" };
        private static readonly string[] CodeHeaders = { "code", "itemcode", "productcode", "sku", "itemno", "productno" };
        private static readonly string[] QuantityHeaders = { "qty", "quantity", "qtydelivered", "qtysupplied", "qtyshipped", "quantitydelivered" };
        private static readonly string[] UnitHeaders = { "unit", "units", "uom", "unitofmeasure" };
        private static readonly string[] PriceHeaders = { "unitprice", "price", "rate", "unitcost", "priceperunit" };
        private static readonly string[] AmountHeaders = { "amount", "total", "linetotal", "value", "extendedprice" };
        private static readonly string[] SummaryRows = { "total", "subtotal", "vat", "tax", "grandtotal", "totaldue", "balancedue" };

        // Reads item lines from a table whose header row names the
        // columns. Needs at least a description and a quantity column.
        public static List<InvoiceScanItem> ItemsFromTable(IList<TableRow> rows)
        {
            var items = new List<InvoiceScanItem>();
            if (rows == null || rows.Count < 2) return items;

            var header = rows.FirstOrDefault(r => r.IsHeader) ?? rows[0];
            var names = header.Cells.Select(HeaderKey).ToList();

            int desc = Column(names, DescriptionHeaders);
            int qty = Column(names, QuantityHeaders);
            if (desc < 0 || qty < 0) return items;

            int code = Column(names, CodeHeaders);
            int unit = Column(names, UnitHeaders);
            int price = Column(names, PriceHeaders);
            int amount = Column(names, AmountHeaders);

            foreach (var row in rows.Where(r => r != header && !r.IsHeader))
            {
                var description = Clean(Cell(row, desc));
                if (description == null || SummaryRows.Contains(HeaderKey(description))) continue;

                var item = new InvoiceScanItem
                {
                    Description = description,
                    ProductCode = Clean(Cell(row, code)),
                    Quantity = ParseNumber(Cell(row, qty)),
                    Unit = Clean(Cell(row, unit)),
                    UnitPrice = ParseNumber(Cell(row, price)),
                    Amount = ParseNumber(Cell(row, amount))
                };

                if (IsUsable(item)) items.Add(item);
            }

            return items;
        }

        private static string HeaderKey(string text)
        {
            return new string((text ?? "").ToLowerInvariant().Where(char.IsLetter).ToArray());
        }

        private static int Column(List<string> names, string[] wanted)
        {
            return names.FindIndex(n => wanted.Contains(n));
        }

        private static string Cell(TableRow row, int index)
        {
            return index >= 0 && index < row.Cells.Count ? row.Cells[index] : null;
        }

        // "R 1 234.50", "1,234.50", "12,5" → number; anything else → null
        public static decimal? ParseNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var s = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-').ToArray());
            if (s.Length == 0 || !s.Any(char.IsDigit)) return null;

            if (s.Contains('.')) s = s.Replace(",", "");
            else if (s.Count(c => c == ',') == 1 && s.Length - s.IndexOf(',') - 1 != 3) s = s.Replace(",", ".");
            else s = s.Replace(",", "");

            decimal parsed;
            return decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out parsed)
                ? parsed : (decimal?)null;
        }

        // ── Field readers: tolerate missing / differently typed fields ──

        private static string Text(IReadOnlyDictionary<string, DocumentField> fields, string name)
        {
            DocumentField f;
            if (!fields.TryGetValue(name, out f) || f == null) return null;

            try
            {
                if (f.FieldType == DocumentFieldType.String) return Clean(f.Value.AsString());
            }
            catch { }

            return Clean(f.Content);
        }

        private static DateTime? Date(IReadOnlyDictionary<string, DocumentField> fields, string name)
        {
            DocumentField f;
            if (!fields.TryGetValue(name, out f) || f == null) return null;

            try
            {
                if (f.FieldType == DocumentFieldType.Date) return f.Value.AsDate().Date;
            }
            catch { }

            DateTime parsed;
            return DateTime.TryParse(f.Content, out parsed) ? parsed.Date : (DateTime?)null;
        }

        private static decimal? Number(IReadOnlyDictionary<string, DocumentField> fields, string name)
        {
            DocumentField f;
            if (!fields.TryGetValue(name, out f) || f == null) return null;

            try
            {
                if (f.FieldType == DocumentFieldType.Double) return (decimal)f.Value.AsDouble();
                if (f.FieldType == DocumentFieldType.Int64) return f.Value.AsInt64();
            }
            catch { }

            decimal parsed;
            return decimal.TryParse((f.Content ?? "").Replace(",", "."), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out parsed) ? parsed : (decimal?)null;
        }

        private static decimal? Money(IReadOnlyDictionary<string, DocumentField> fields, string name)
        {
            DocumentField f;
            if (!fields.TryGetValue(name, out f) || f == null) return null;

            try
            {
                if (f.FieldType == DocumentFieldType.Currency) return (decimal)f.Value.AsCurrency().Amount;
            }
            catch { }

            return Number(fields, name);
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Replace("\n", " ").Trim();
            return s.Length > 200 ? s.Substring(0, 200) : s;
        }
    }
}
