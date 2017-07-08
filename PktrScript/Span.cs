using System;


namespace PktrScript
{
    public class Span
    {
        public string filename;
        public int start, end;
        public int startLine, startColumn;


        public Span(string filename, int start, int end, int startLine, int startColumn)
        {
            this.filename = filename;
            this.start = start;
            this.end = end;
            this.startLine = startLine;
            this.startColumn = startColumn;
        }


        public int Length
        {
            get { return this.end - this.start; }
        }


        public Span JustBefore
        {
            get { return new Span(this.filename, this.start, this.start, this.startLine, this.startColumn); }
        }


        public Span JustAfter
        {
            get { return new Span(this.filename, this.end, this.end, this.startLine, this.startColumn); }
        }


        public static Span operator +(Span a, Span b)
        {
            if (a == null)
                return new Span(b.filename, b.start, b.end, b.startLine, b.startColumn);

            if (b == null)
                return new Span(a.filename, a.start, a.end, a.startLine, a.startColumn);

            if (a.filename != b.filename)
                throw new Exception("spans point to different units");

            return new Span(
                a.filename,
                Math.Min(a.start, b.start),
                Math.Max(a.end, b.end),
                (a.start < b.start) ? a.startLine : b.startLine,
                (a.start < b.start) ? a.startColumn : b.startColumn);
        }
    }
}
