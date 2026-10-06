using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Michaelhouse.Services
{
    // ============================================================
    // Test supplier invoice for a purchase order
    //
    // FOR TESTING the Record Stock Delivery workflow end to end:
    //   confirm order → test invoice (PDF) → download / print / photo
    //   → Record Delivery scan (Azure Document Intelligence) →
    //   matching → manager confirms → stock goes up.
    // It never records a delivery or changes stock itself, and it is
    // not an accounting document.
    //
    // The PDF shows what the supplier is still expected to deliver:
    // confirmed quantity − already received, per line. It carries the
    // supplier's details, an invoice number, the date and our PO
    // number (how IngredientDeliveryService finds the order), and each
    // item as the supplier names it (IngredientSupplier.SupplierItemName,
    // else the ingredient name) with quantity and kg / L unit, so
    // IngredientMatchingService and the unit conversion work as for a
    // real invoice.
    //
    // Refresh is called after every change to a confirmed order
    // (confirmation, amendment, shortfall decision, delivery):
    //   • invoice number not yet used by a recorded delivery → the same
    //     number is re-issued with the current quantities
    //   • already used by a delivery → that invoice stays as it was
    //     (history); a new revision (…-B, …-C) covers what's still
    //     outstanding. Deliveries reject a reused invoice number, so
    //     this also keeps the next delivery recordable.
    // A shortfall re-ordered from another supplier is its own order,
    // with its own invoice once that supplier confirms.
    // ============================================================
    public class TestInvoiceService
    {
        private readonly DBContextClass _db;
        private readonly Func<DateTime> _now;
        private readonly InvoiceScanService _files;

        public TestInvoiceService(DBContextClass db, Func<DateTime> now = null, InvoiceScanService files = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _now = now ?? (() => SchoolClock.Now);
            _files = files ?? new InvoiceScanService();
        }

        // The school, as the customer on the invoice
        private const string CustomerName = "Michaelhouse Cafeteria";
        private static readonly string[] CustomerAddress = { "Michaelhouse", "Balgowan", "KwaZulu-Natal, 3275" };

        // Creates / updates the order's test invoice when appropriate.
        // Saves the order. Returns a message for the manager, or null
        // when nothing was needed. Never throws: an invoice problem
        // mustn't block confirming or amending the order.
        public string Refresh(int orderId)
        {
            try
            {
                var order = _db.IngredientPurchaseOrders
                    .Include("Supplier")
                    .Include("Lines.Ingredient")
                    .FirstOrDefault(o => o.Id == orderId);

                if (order == null) return null;

                // Only once the supplier has confirmed, and while
                // something is still to come
                if (order.Status != IngredientOrderStatus.Confirmed && order.Status != IngredientOrderStatus.PartiallyFulfilled)
                    return null;

                var outstanding = order.Lines.Where(l => l.Outstanding > 0m).OrderBy(l => l.Ingredient.Name).ToList();
                if (outstanding.Count == 0) return null;

                string number = NumberFor(order);
                bool reissue = number == order.TestInvoiceNumber;

                var links = _db.IngredientSuppliers
                    .Where(x => x.SupplierId == order.SupplierId)
                    .ToList()
                    .GroupBy(x => x.IngredientId)
                    .ToDictionary(g => g.Key, g => g.First());

                var pdf = BuildPdf(order, outstanding, links, number, _now());
                var path = _files.SaveGenerated(pdf, ".pdf");
                if (path == null) return "The test invoice couldn't be saved (file storage isn't configured).";

                // Replace the previous file of the same invoice number,
                // unless a delivery kept it as its scanned invoice
                if (reissue && order.TestInvoiceFilePath != null
                    && !_db.IngredientDeliveries.Any(d => d.InvoiceFilePath == order.TestInvoiceFilePath))
                {
                    var old = _files.FullPath(order.TestInvoiceFilePath);
                    if (old != null && File.Exists(old)) File.Delete(old);
                }

                order.TestInvoiceNumber = number;
                order.TestInvoiceFilePath = path;
                order.TestInvoiceGeneratedAt = DateTime.UtcNow;
                _db.SaveChanges();

                return reissue
                    ? "Test invoice " + number + " updated to match the order."
                    : "Test invoice " + number + " generated for this order.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("Test invoice for order " + orderId + " failed: " + ex.Message);
                return "The test invoice couldn't be generated: " + ex.Message;
            }
        }

        // INV-TEST-2026-00012 for CAF-PO-2026-00012; a letter suffix once
        // a delivery has used the current number
        private string NumberFor(IngredientPurchaseOrder order)
        {
            var current = order.TestInvoiceNumber;
            if (current != null && !UsedByDelivery(order.SupplierId, current)) return current;

            string baseNumber = "INV-TEST-" + (order.PoNumber.StartsWith("CAF-PO-") ? order.PoNumber.Substring(7) : order.PoNumber);
            if (current == null && !UsedByDelivery(order.SupplierId, baseNumber)) return baseNumber;

            for (char c = 'B'; c <= 'Z'; c++)
            {
                var candidate = baseNumber + "-" + c;
                if (!UsedByDelivery(order.SupplierId, candidate) && candidate != current) return candidate;
            }

            return baseNumber + "-" + DateTime.UtcNow.ToString("HHmmss");
        }

        private bool UsedByDelivery(int supplierId, string number)
        {
            return _db.IngredientDeliveries.Any(d => d.SupplierId == supplierId && d.InvoiceNumber == number);
        }

        // ============================================================
        // PDF
        // ============================================================

        private static byte[] BuildPdf(
            IngredientPurchaseOrder order,
            List<IngredientPurchaseOrderLine> lines,
            Dictionary<int, IngredientSupplier> links,
            string number,
            DateTime date)
        {
            var inv = CultureInfo.InvariantCulture;
            var pdf = new SimplePdf();
            var s = order.Supplier;

            const float left = 50f, right = 545f;

            // Columns: Description | Qty | Unit | Unit price | Amount
            const float colQty = 350f, colUnit = 362f, colPrice = 470f, colAmount = right;

            float y = 790f;

            Action newPageHeader = () =>
            {
                pdf.Text(left, 800f, 9f, false, s.Name + "  ·  Invoice " + number + " (continued)");
                y = 770f;
            };

            // ── Supplier ──
            pdf.Text(left, y, 18f, true, s.Name);
            y -= 16f;
            foreach (var part in AddressLines(s.Address))
            {
                pdf.Text(left, y, 9.5f, false, part);
                y -= 12f;
            }
            if (!string.IsNullOrWhiteSpace(s.Phone)) { pdf.Text(left, y, 9.5f, false, "Tel: " + s.Phone.Trim()); y -= 12f; }
            if (!string.IsNullOrWhiteSpace(s.Email)) { pdf.Text(left, y, 9.5f, false, "Email: " + s.Email.Trim()); y -= 12f; }

            // ── Title + invoice details (right) ──
            pdf.TextRight(right, 790f, 20f, true, "INVOICE");
            float dy = 768f;
            Action<string, string> detail = (label, value) =>
            {
                pdf.Text(380f, dy, 9.5f, true, label);
                pdf.TextRight(right, dy, 9.5f, false, value);
                dy -= 13f;
            };
            detail("Invoice No:", number);
            detail("Invoice Date:", date.ToString("dd/MM/yyyy", inv));
            detail("PO Number:", order.PoNumber);
            if (order.RequestedDeliveryDate.HasValue) detail("Delivery Date:", order.RequestedDeliveryDate.Value.ToString("dd/MM/yyyy", inv));

            y = Math.Min(y, dy) - 14f;

            // ── Customer ──
            pdf.Text(left, y, 9.5f, true, "Bill To:");
            y -= 12f;
            pdf.Text(left, y, 9.5f, false, CustomerName);
            y -= 12f;
            foreach (var part in CustomerAddress) { pdf.Text(left, y, 9.5f, false, part); y -= 12f; }

            y -= 14f;

            // ── Items ──
            Action tableHeader = () =>
            {
                pdf.Line(left, y + 12f, right, y + 12f, 0.8f);
                pdf.Text(left, y, 9.5f, true, "Description");
                pdf.TextRight(colQty, y, 9.5f, true, "Quantity");
                pdf.Text(colUnit, y, 9.5f, true, "Unit");
                pdf.TextRight(colPrice, y, 9.5f, true, "Unit Price");
                pdf.TextRight(colAmount, y, 9.5f, true, "Amount");
                pdf.Line(left, y - 5f, right, y - 5f, 0.8f);
                y -= 20f;
            };

            tableHeader();

            decimal total = 0m;
            bool allPriced = true;

            foreach (var line in lines)
            {
                if (y < 90f)
                {
                    pdf.NewPage();
                    newPageHeader();
                    tableHeader();
                }

                var ingredient = line.Ingredient;
                IngredientSupplier link;
                links.TryGetValue(ingredient.Id, out link);

                string description = link != null && !string.IsNullOrWhiteSpace(link.SupplierItemName)
                    ? link.SupplierItemName.Trim()
                    : ingredient.Name;

                decimal qty = IngredientUnits.ToDisplay(line.Outstanding, ingredient.Unit);
                string unit = IngredientUnits.DisplayUnit(ingredient.Unit);

                // Price per kg / L: the order's, else the supplier link's
                decimal? price = line.UnitCost ?? (link != null ? link.UnitCost : null);

                pdf.Text(left, y, 10f, false, Clip(description, 52));
                pdf.TextRight(colQty, y, 10f, false, qty.ToString("0.###", inv));
                pdf.Text(colUnit, y, 10f, false, unit);

                if (price.HasValue)
                {
                    decimal amount = Math.Round(qty * price.Value, 2);
                    total += amount;
                    pdf.TextRight(colPrice, y, 10f, false, "R " + price.Value.ToString("0.00", inv));
                    pdf.TextRight(colAmount, y, 10f, false, "R " + amount.ToString("0.00", inv));
                }
                else
                {
                    allPriced = false;
                }

                y -= 16f;
            }

            pdf.Line(left, y + 8f, right, y + 8f, 0.8f);
            y -= 8f;

            if (allPriced && lines.Count > 0)
            {
                pdf.Text(colUnit, y, 11f, true, "Total");
                pdf.TextRight(colAmount, y, 11f, true, "R " + total.ToString("0.00", inv));
                y -= 22f;
            }

            pdf.Text(left, Math.Max(y - 10f, 60f), 8f, false,
                "Test invoice generated by the Michaelhouse system to test cafeteria deliveries. Not a tax invoice.");

            return pdf.ToBytes();
        }

        private static IEnumerable<string> AddressLines(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return Enumerable.Empty<string>();
            return address
                .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Take(4);
        }

        private static string Clip(string value, int max)
        {
            return value.Length > max ? value.Substring(0, max - 1) + "…" : value;
        }
    }

    // ============================================================
    // A minimal PDF writer: text in Helvetica / Helvetica-Bold and
    // straight lines on A4 pages. Enough for a clear, machine-readable
    // invoice without adding a PDF library to the project.
    // ============================================================
    internal class SimplePdf
    {
        private readonly List<StringBuilder> _pages = new List<StringBuilder>();
        private StringBuilder _page;
        private static readonly Encoding WinAnsi = Encoding.GetEncoding(1252);
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public SimplePdf()
        {
            NewPage();
        }

        public void NewPage()
        {
            _page = new StringBuilder();
            _pages.Add(_page);
        }

        public void Text(float x, float y, float size, bool bold, string text)
        {
            _page.AppendFormat(Inv, "BT /{0} {1:0.##} Tf {2:0.##} {3:0.##} Td ({4}) Tj ET\n",
                bold ? "F2" : "F1", size, x, y, Escape(text));
        }

        public void TextRight(float rightX, float y, float size, bool bold, string text)
        {
            Text(rightX - Width(text, size, bold), y, size, bold, text);
        }

        public void Line(float x1, float y1, float x2, float y2, float width)
        {
            _page.AppendFormat(Inv, "{0:0.##} w {1:0.##} {2:0.##} m {3:0.##} {4:0.##} l S\n", width, x1, y1, x2, y2);
        }

        // Helvetica advance widths (per 1000) for the characters used in
        // numbers / short labels; anything else is an average
        private static float Width(string text, float size, bool bold)
        {
            float units = 0f;
            foreach (char c in text ?? "")
            {
                if (char.IsDigit(c)) units += 556f;
                else if (c == '.' || c == ',' || c == ' ' || c == ':' ) units += 278f;
                else if (c == '-') units += 333f;
                else if (c == 'R') units += 722f;
                else if (char.IsUpper(c)) units += bold ? 722f : 667f;
                else units += bold ? 556f : 500f;
            }
            return units * size / 1000f;
        }

        private static string Escape(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text ?? "")
            {
                if (c == '\\' || c == '(' || c == ')') sb.Append('\\').Append(c);
                else if (c == '·') sb.Append("\\267");
                else if (c == '…') sb.Append("\\205");
                else if (c < 32) sb.Append(' ');
                else if (c > 126)
                {
                    var b = WinAnsi.GetBytes(c.ToString());
                    sb.Append(b.Length == 1 && b[0] != (byte)'?' ? "\\" + Convert.ToString(b[0], 8) : "?");
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public byte[] ToBytes()
        {
            // Objects: 1 catalog, 2 pages, 3 F1, 4 F2, then per page: page + content
            var objects = new List<string>();
            int pageCount = _pages.Count;
            var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => (5 + i * 2) + " 0 R"));

            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add("<< /Type /Pages /Kids [" + kids + "] /Count " + pageCount + " >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

            var streams = new List<byte[]>();
            for (int i = 0; i < pageCount; i++)
            {
                int contentObj = 6 + i * 2;
                objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents " + contentObj + " 0 R >>");
                var content = Encoding.ASCII.GetBytes(_pages[i].ToString());
                streams.Add(content);
                objects.Add(null);   // placeholder: stream written below
            }

            using (var ms = new MemoryStream())
            {
                var offsets = new List<long>();
                Action<string> write = str => { var b = Encoding.ASCII.GetBytes(str); ms.Write(b, 0, b.Length); };

                write("%PDF-1.4\n");
                ms.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }, 0, 6);

                int streamIndex = 0;
                for (int n = 0; n < objects.Count; n++)
                {
                    offsets.Add(ms.Position);
                    write((n + 1) + " 0 obj\n");

                    if (objects[n] != null)
                    {
                        write(objects[n] + "\n");
                    }
                    else
                    {
                        var data = streams[streamIndex++];
                        write("<< /Length " + data.Length + " >>\nstream\n");
                        ms.Write(data, 0, data.Length);
                        write("\nendstream\n");
                    }

                    write("endobj\n");
                }

                long xref = ms.Position;
                write("xref\n0 " + (objects.Count + 1) + "\n0000000000 65535 f \n");
                foreach (var o in offsets) write(o.ToString("0000000000") + " 00000 n \n");
                write("trailer\n<< /Size " + (objects.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");

                return ms.ToArray();
            }
        }
    }
}
