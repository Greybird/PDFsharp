// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Diagnostics;
using System.Globalization;
#if !NET6_0_OR_GREATER
using System.Net.Http;
#endif
#if WPF
using System.IO;
#endif
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfSharp.Quality;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using PdfSharp.Logging;
using Xunit.Abstractions;

namespace PdfSharp.Tests.IO
{
    [Collection("PDFsharp")]
    public class ReaderTests
    {
        [Fact]
        public void Read_empty_file()
        {
            var data = Array.Empty<byte>();
            using var stream = new MemoryStream(data);

            // A file with 0 bytes is not valid and an exception should occur.
            // ReSharper disable once AccessToDisposedClosure
            Action open = () => PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
            open.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Read_invalid_file()
        {
            var data = new byte[32];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(33 + i);

            using var stream = new MemoryStream(data);

            // A file with 32 random bytes is not valid and an exception should occur.
            // ReSharper disable once AccessToDisposedClosure
            Action open = () => PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
            open.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Write_and_read_test()
        {
            var doc = new PdfDocument();
            doc.AddPage();
            doc.Info.Author = "empira";
            doc.Info.Title = "Test";

            using var stream = new MemoryStream();
            doc.Save(stream, false);
            stream.Position = 0;
            stream.Length.Should().BeGreaterThan(0);

            var doc2 = PdfReader.Open(stream, PdfDocumentOpenMode.Modify);

            using var stream2 = new MemoryStream();
            doc2.Save(stream2, false);
            stream2.Position = 0;
            stream2.Length.Should().BeGreaterThan(0);

            var doc3 = PdfReader.Open(stream2, PdfDocumentOpenMode.Modify);

            using var stream3 = new MemoryStream();
            doc3.Save(stream3, false);
            stream3.Position = 0;
            stream3.Length.Should().BeGreaterThan(0);

            doc3.Info.Author.Should().Be("empira");
            doc3.Info.Title.Should().Be("Test");
        }

        [Fact]
        public void Read_reals_and_integers_test()
        {
            // Checks Lexer::ScanNumber with various number formats.
            var doc = new PdfDocument();
            var page = doc.AddPage();
            doc.Info.Author = "empira";
            doc.Info.Title = "Test";

            // Now add many different numbers for testing.
            var dict = new PdfDictionary(doc);
            var array = new PdfArray(doc);
            dict.Elements["/empiraPlayground"] = array;
            array.Elements.Add(new PdfLiteral("-32768"));
            array.Elements.Add(new PdfLiteral("-2147483648")); // Int32
            array.Elements.Add(new PdfLiteral("-2147483649")); // Int64
            array.Elements.Add(new PdfLiteral("-2147483648.0")); // Real
            array.Elements.Add(new PdfLiteral("-2147483649.0"));
            array.Elements.Add(new PdfLiteral("-2147483648."));
            array.Elements.Add(new PdfLiteral("-2147483649."));
            array.Elements.Add(new PdfLiteral(".123"));
            array.Elements.Add(new PdfLiteral("-.456"));
            array.Elements.Add(new PdfLiteral("-2147483648.123"));
            array.Elements.Add(new PdfLiteral("-2147483649.123"));
            array.Elements.Add(new PdfLiteral("123.4567890123456789"));
            array.Elements.Add(new PdfLiteral("1234567890.1234567890"));
            // This line causes an error message in Adobe Reader. array.Elements.Add(new PdfLiteral("12345678901234567890.12345678901234567890"));
            array.Elements.Add(new PdfLiteral("-9223372036854775808")); // Int64
            doc.Internals.AddObject(dict);
            doc.Internals.Catalog.Elements["/empiraPlayground"] = PdfInternals.GetReference(dict);

            page.Resources.Elements["/NumberTest"] = array;

            //array = new PdfArray(doc);
            //array.Elements.Add(new PdfInteger(-32768));
            //array.Elements.Add(new PdfIntegerObject(-32768));
            //array.Elements.Add(new PdfReal(17));
            //array.Elements.Add(new PdfRealObject(17));
            //array.Elements.Add(new PdfReal(17.7));
            //array.Elements.Add(new PdfRealObject(17.7));
            //array.Elements.Add(new PdfString("Foo"));
            //array.Elements.Add(new PdfStringObject("Foo_äöüß", PdfStringEncoding.PDFDocEncoding));
            //array.Elements.Add(new PdfStringObject("Foo_äöüß", PdfStringEncoding.WinAnsiEncoding));
            //array.Elements.Add(new PdfStringObject("Foo_äöüß", PdfStringEncoding.Unicode));
            //page.Resources.Elements["/ReferenceTest"] = array;

            using var stream = new MemoryStream();
            doc.Save(stream, false);
            stream.Position = 0;
            stream.Length.Should().BeGreaterThan(0);

            var doc2 = PdfReader.Open(stream, PdfDocumentOpenMode.Modify);

            // Now check if the numbers are correct.
            page = doc2.Pages[0];
            var array2 = page.Resources.Elements["/NumberTest"] as PdfArray;
            Debug.Assert(array2 != null, nameof(array2) + " != null");
            for (int x = 0; x < array2!.Elements.Count; ++x)
            {
                var item1 = array.Elements[x];
                var item2 = array2.Elements[x];
                var num2 = Double.Parse(item1.ToString()!, CultureInfo.InvariantCulture);
                if (item2 is PdfInteger intValue)
                {
                    num2.Should().Be(intValue.Value);
                }
                else if (item2 is PdfLongInteger longValue)
                {
                    num2.Should().Be(longValue.Value);
                }
                else if (item2 is PdfReal realValue)
                {
                    num2.Should().Be(realValue.Value);
                }
                else
                {
                    1.Should().Be(2, "Should not come here!");
                }
            }

            using var stream2 = new MemoryStream();
            doc2.Save(stream2, false);
            stream2.Position = 0;
            stream2.Length.Should().BeGreaterThan(0);

            var doc3 = PdfReader.Open(stream2, PdfDocumentOpenMode.Modify);

            using var stream3 = new MemoryStream();
            doc3.Save(stream3, false);
            stream3.Position = 0;
            stream3.Length.Should().BeGreaterThan(0);

            doc3.Info.Author.Should().Be("empira");
            doc3.Info.Title.Should().Be("Test");
        }

        [Fact]
        public void Custom_properties_test()
        {
            // Checks custom properties with non-ASCII chars.
            var doc = new PdfDocument();
            var page = doc.AddPage();
            doc.Info.Author = "empira";
            doc.Info.Title = "Test";
#if true
            var properties = doc.Info.Elements;
            properties.SetString("/Test", "Test");
            properties.SetString("/UmlautTestÄÖÜäöüß", "UmlautTestÄÖÜäöüß");
            properties.SetString("/ÖÇŞİĞÜüğişçö", "ÖÇŞİĞÜüğişçö");
            properties.SetString("/فثسف", "فثسف");
            properties.SetString(@"/é£½€¨´", @"é£½€¨´");
            properties.SetString("/A#20B", "A#20B");
            properties.SetString("/A B", "A B");
            properties.SetString("/A()B", "A()B");
            properties.SetString("/A<>B", "A<>B");
            properties.SetString("/A<<>>B", "A<<>>B");
            properties.SetString("/AÀÁÂÃÄÅÆÇÈÉÊËB", "AÀÁÂÃÄÅÆÇÈÉÊËB");
            properties.SetString("/🖕 🦄 🦂 🍇 🍆 ☕ 🚂 🛸 ☁ ☢ ♌ ♏ ✅ ☑ ✔ ™ 🆒",
                "🖕 🦄 🦂 🍇 🍆 ☕ 🚂 🛸 ☁ ☢ ♌ ♏ ✅ ☑ ✔ ™ 🆒");
            properties.SetString("/✓✔✅🐛👌🆗", "✓✔✅🐛👌🆗");
#endif
            using var stream = new MemoryStream();
            doc.Save(stream, false);
            stream.Position = 0;
            stream.Length.Should().BeGreaterThan(0);

            var doc2 = PdfReader.Open(stream, PdfDocumentOpenMode.Modify);

            using var stream2 = new MemoryStream();
            doc2.Save(stream2, false);
            stream2.Position = 0;
            stream2.Length.Should().BeGreaterThan(0);

            var doc3 = PdfReader.Open(stream2, PdfDocumentOpenMode.Modify);

            using var stream3 = new MemoryStream();
            doc3.Save(stream3, false);
            stream3.Position = 0;
            stream3.Length.Should().BeGreaterThan(0);

            doc3.Info.Author.Should().Be("empira");
            doc3.Info.Title.Should().Be("Test");

            var doc4 = PdfReader.Open(stream3, PdfDocumentOpenMode.Modify);
            var filename = PdfFileUtility.GetTempPdfFileName("Custom_properties");
            doc4.Save(filename);
            PdfFileUtility.ShowDocumentIfDebugging(filename);
        }

        [Fact]
        public void Read_and_modify_PDF_file_PageOrientation_test()
        {
            // This test creates all 8 possible variations of A4 pages.
            // Step 1 draws a red frame around the page.
            // Step 2 draws a narrower, yellow frame around the page.
            // Step 3 draws a narrow, green frame around the page.
            // All frames should be visible after step 3 and should be around the edges of the pages.
            var document = new PdfDocument();

            // There are 4 possible values for rotation and 2 options for orientation.
            // We create 8 pages to cover all cases.
            for (int i = 0; i < 8; ++i)
            {
                var page = document.AddPage();
                page.Size = PageSize.A4;
                page.Rotate = i % 4 * 90;
                page.Orientation = (i / 4) switch
                {
                    0 => PageOrientation.Portrait,
                    1 => PageOrientation.Landscape,
                    _ => throw new ArgumentOutOfRangeException()
                };
                DrawPageFrame(page, XColors.Red, 20,
                    $"Page {i + 1} - Rotate {page.Rotate} - Orientation {page.Orientation}");
            }

            // Save the document...
            string filename = PdfFileUtility.GetTempPdfFullFileName("unittests/IO/ReaderTests/OrientationTestStep1");
            document.Save(filename);

            // Read the document from step 1.
            document = PdfReader.Open(filename, PdfDocumentOpenMode.Modify);
            for (int i = 0; i < 8; ++i)
            {
                var page = document.Pages[i];
#if true_ // can be deleted now
                // Modifying the PDF works with this hack.
                page.Orientation = PageOrientation.Portrait; // Always set "Portrait" for imported pages.
#endif
                DrawPageFrame(page, XColors.Yellow, 10);
            }

            // Save the document...
            filename = PdfFileUtility.GetTempPdfFullFileName("unittests/IO/ReaderTests/OrientationTestStep2");
            document.Save(filename);

            // Read the document from step 2.
            document = PdfReader.Open(filename, PdfDocumentOpenMode.Modify);
            for (int i = 0; i < 8; ++i)
            {
                var page = document.Pages[i];
                DrawPageFrame(page, XColors.Green, 5);
            }

            // Save the document...
            filename = PdfFileUtility.GetTempPdfFullFileName("unittests/IO/ReaderTests/OrientationTestStep3");
            document.Save(filename);

            static void DrawPageFrame(PdfPage page, XColor color, Int32 width, string? label = null)
            {
                // Draw a frame around the page. Add an optional label.
                using var gfx = XGraphics.FromPdfPage(page);
                var pen = new XPen(XColors.OrangeRed, 10) { LineCap = XLineCap.Round };
                gfx.DrawLine(pen, 0, 0, 200, 200);
                gfx.DrawRectangle(new XPen(color, width), new XRect(0, 0, page.Width.Point, page.Height.Point));
                if (!String.IsNullOrWhiteSpace(label))
                {
                    var font = new XFont("Verdana", 10, XFontStyleEx.Regular);
                    gfx.DrawString(label ?? "", font, XBrushes.Firebrick, 20, 20);
                }
            }
        }

        // [Theory]
        // [InlineData("https://github.com/py-pdf/pypdf/blob/0c81f3cfad26ddffbfc60d0ae855118e515fad8c/resources/encryption/r5-empty-password.pdf?raw=true", PdfDocumentOpenMode.Import, null)]
        // [InlineData("https://github.com/py-pdf/pypdf/blob/0c81f3cfad26ddffbfc60d0ae855118e515fad8c/resources/encryption/r5-user-password.pdf?raw=true", PdfDocumentOpenMode.Import, "asdfzxcv")]
        // [InlineData("https://github.com/py-pdf/pypdf/blob/0c81f3cfad26ddffbfc60d0ae855118e515fad8c/resources/encryption/r5-owner-password.pdf?raw=true", PdfDocumentOpenMode.Modify, "asdfzxcv")]
        // [InlineData("https://github.com/py-pdf/pypdf/blob/0c81f3cfad26ddffbfc60d0ae855118e515fad8c/resources/encryption/r5-owner-password.pdf?raw=true", PdfDocumentOpenMode.Import, "asdfzxcv")]
        // public async Task Read_file_with_version_5_revision_5_encryption(string url, PdfDocumentOpenMode mode, string? password)
        // {
        //     var client = new HttpClient();
        //     var content = await client.GetByteArrayAsync(url);
        //     using var ms = new MemoryStream(content);
        //
        //     var act = () => PdfReader.Open(ms, password, mode);
        //
        //     act.Should().NotThrow();
        // }

        [Fact]
        public void ReverseSolidus_with_invalid_following_character_should_be_ignored()
        {
            using var doc = PdfReader.Open(@"C:\Users\ArnaudTAMAILLON\Downloads\Cover-letter-4098208.pdf");
            var producer = doc.Info.Producer;
            producer.Should().Be("C48x Series (PDF - 300X300 dpi)");
        }
        //
        // [Theory]
        // [MemberData(nameof(Should_parse_files_source))]
        // public void Should_parse_files(string name)
        // {
        //     using var doc = PdfReader.Open(name, PdfDocumentOpenMode.Import);
        //     var elements    = doc?.Info?.Elements;
        // }
        //
        // public static TheoryData<string> Should_parse_files_source()
        // {
        //     var di = new DirectoryInfo(@"C:\Code\Data\DocumentAnalysis\Files");
        //     return new TheoryData<string>(di.GetFiles().Select(f => f.FullName));
        // }

        [Fact]
        public void Encrypt_can_be_an_inline_dictionary()
        {
            const string path = @"C:\Users\ArnaudTAMAILLON\Downloads\dictionary_encrypt.pdf";
            var act = () => PdfReader.Open(path, PdfDocumentOpenMode.Import);
            act.Should().NotThrow();
            act = () => PdfReader.Open(path, "asdfzxcv", PdfDocumentOpenMode.Modify);
            act.Should().NotThrow();
        }

        [Fact]
        public void Test_Comma_In_Real()
        {
            const string path =
                @"C:\Code\Data\DocumentAnalysis\250820241610\Files\00247316-e4d3-441d-bd62-3ad9e95df232.pdf";
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var elements    = doc?.Info?.Elements;
        }

        [Fact]
        public void Test_Large_Object_Id()
        {
            const string path =
                @"C:\Code\Data\DocumentAnalysis\260820240741\Files\0a2fc4c0-e545-43ff-95e0-ee4aabc63fa6.pdf";
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var elements    = doc?.Info?.Elements;
        }

        [Fact]
        public void Test_Missing_EndObj()
        {
            const string path =
                @"C:\Code\Data\DocumentAnalysis\260820240741\Files\01b5f2c7-6040-40b5-ac95-29b301a3102b.pdf";
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var elements    = doc?.Info?.Elements;
        }

        [Fact]
        public void Test_Incorrect_indirect_object_declaration()
        {
            const string path =
                @"C:\Code\Data\DocumentAnalysis\260820240741\Files\0a6e76ef-adf3-4832-af9a-1a192ab0540e.pdf";
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var elements    = doc?.Info?.Elements;
        }

        [Fact]
        public void Test_references_in_indirected_references()
        {
            const string path =
                @"C:\Code\Data\DocumentAnalysis\260820240741\Files\0b12fd65-95b6-4ec2-af5f-7f0e64077ebc.pdf";
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var elements    = doc?.Info?.Elements;
        }

        [Fact]
        public void Test_Trailer_Prev_Zero()
        {
            const string path =
                @"C:\Code\Data\DocumentAnalysis\260820240741\Files\0be07d7c-6eb7-44db-aac7-2d314b39f09f.pdf";
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var elements    = doc?.Info?.Elements;
        }

        [Fact]
        public void Test_Missing_EndStream()
        {
            const string path =
                @"C:\Code\Data\DocumentAnalysis\260820240741\Files\0a7377bc-4737-498f-84f5-d0727e6e5e31.pdf";
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var elements    = doc?.Info?.Elements;
        }
    }
}
