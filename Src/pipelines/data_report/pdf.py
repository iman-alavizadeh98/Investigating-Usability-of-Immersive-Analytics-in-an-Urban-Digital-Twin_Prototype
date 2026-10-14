"""
Small ReportLab layer for the data report: A4 pages, DejaVu fonts, headings,
paragraphs, tables, stat tiles, numbered figures with captions, and a footer
that carries the licence warning on every page.
"""

from __future__ import annotations

from pathlib import Path
from typing import List, Optional, Sequence

from PIL import Image as PILImage
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (BaseDocTemplate, CondPageBreak, Frame, Image, KeepTogether, PageBreak, PageTemplate,
                                Paragraph, Spacer, Table, TableStyle)
from reportlab.platypus.tableofcontents import TableOfContents

from . import style as S

MARGIN = 18 * mm
FRAME_W = A4[0] - 2 * MARGIN
FRAME_H = A4[1] - 2 * MARGIN - 8 * mm


def _register_fonts() -> None:
    if "DejaVu" in pdfmetrics.getRegisteredFontNames():
        return
    p = S.dejavu_paths()
    pdfmetrics.registerFont(TTFont("DejaVu", str(p["regular"])))
    pdfmetrics.registerFont(TTFont("DejaVu-Bold", str(p["bold"])))
    pdfmetrics.registerFont(TTFont("DejaVu-Oblique", str(p["oblique"])))
    pdfmetrics.registerFontFamily("DejaVu", normal="DejaVu", bold="DejaVu-Bold", italic="DejaVu-Oblique",
                                  boldItalic="DejaVu-Bold")


_register_fonts()
INK = colors.HexColor(S.INK)
INK2 = colors.HexColor(S.INK_2)
INK3 = colors.HexColor(S.INK_3)
RULE = colors.HexColor(S.GRID)
TINT = colors.HexColor("#f4f3f0")
ACCENT = colors.HexColor(S.CAT[0])
WARN = colors.HexColor("#fdf3e1")

ST = {
    "title": ParagraphStyle("title", fontName="DejaVu-Bold", fontSize=24, leading=29, textColor=INK, spaceAfter=6),
    "subtitle": ParagraphStyle("subtitle", fontName="DejaVu", fontSize=11.5, leading=16, textColor=INK2, spaceAfter=14),
    "h1": ParagraphStyle("h1", fontName="DejaVu-Bold", fontSize=15, leading=19, textColor=INK, spaceBefore=4, spaceAfter=8),
    "h2": ParagraphStyle("h2", fontName="DejaVu-Bold", fontSize=11, leading=14, textColor=INK, spaceBefore=10, spaceAfter=4),
    "body": ParagraphStyle("body", fontName="DejaVu", fontSize=9, leading=13, textColor=INK, spaceAfter=5, alignment=TA_LEFT),
    "bullet": ParagraphStyle("bullet", fontName="DejaVu", fontSize=9, leading=12.5, textColor=INK, leftIndent=11,
                             bulletIndent=2, spaceAfter=2.5),
    "caption": ParagraphStyle("caption", fontName="DejaVu", fontSize=7.8, leading=10.5, textColor=INK2, spaceBefore=3,
                              spaceAfter=10),
    "cell": ParagraphStyle("cell", fontName="DejaVu", fontSize=7.6, leading=9.6, textColor=INK),
    "cellb": ParagraphStyle("cellb", fontName="DejaVu-Bold", fontSize=7.6, leading=9.6, textColor=INK),
    "tile_n": ParagraphStyle("tile_n", fontName="DejaVu-Bold", fontSize=17, leading=20, textColor=INK),
    "tile_l": ParagraphStyle("tile_l", fontName="DejaVu", fontSize=7.6, leading=9.5, textColor=INK2),
    "note": ParagraphStyle("note", fontName="DejaVu", fontSize=8.4, leading=11.5, textColor=INK),
    "toc1": ParagraphStyle("toc1", fontName="DejaVu", fontSize=9.5, leading=15, textColor=INK, leftIndent=0),
}


class _Doc(BaseDocTemplate):
    def __init__(self, path: Path, title: str, footer: str):
        super().__init__(str(path), pagesize=A4, leftMargin=MARGIN, rightMargin=MARGIN, topMargin=MARGIN,
                         bottomMargin=MARGIN + 8 * mm, title=title, author="Data report generator")
        self.footer = footer
        frame = Frame(MARGIN, MARGIN + 8 * mm, FRAME_W, FRAME_H, id="body", leftPadding=0, rightPadding=0,
                      topPadding=0, bottomPadding=0)
        self.addPageTemplates([PageTemplate(id="page", frames=[frame], onPage=self._decorate)])

    def _decorate(self, canv, doc):
        canv.saveState()
        canv.setStrokeColor(RULE)
        canv.setLineWidth(0.5)
        canv.line(MARGIN, MARGIN + 4 * mm, A4[0] - MARGIN, MARGIN + 4 * mm)
        canv.setFont("DejaVu", 7)
        canv.setFillColor(INK3)
        canv.drawString(MARGIN, MARGIN, self.footer)
        canv.drawRightString(A4[0] - MARGIN, MARGIN, f"{doc.page}")
        canv.restoreState()

    def afterFlowable(self, flowable):
        if isinstance(flowable, Paragraph) and flowable.style.name == "h1":
            self.notify("TOCEntry", (0, flowable.getPlainText(), self.page))


class ReportDoc:
    """Collects flowables; numbers figures and tables."""

    def __init__(self):
        self.story: List = []
        self.fig_no = 0
        self.tab_no = 0

    # text
    def title(self, text: str, subtitle: str = ""):
        self.story.append(Spacer(1, 30 * mm))
        self.story.append(Paragraph(text, ST["title"]))
        if subtitle:
            self.story.append(Paragraph(subtitle, ST["subtitle"]))

    def h1(self, text: str, new_page: bool = True):
        if new_page:
            self.story.append(PageBreak())
        self.story.append(Paragraph(text, ST["h1"]))

    def h2(self, text: str):
        self.story.append(CondPageBreak(30 * mm))
        self.story.append(Paragraph(text, ST["h2"]))

    def p(self, text: str):
        self.story.append(Paragraph(text, ST["body"]))

    def bullets(self, items: Sequence[str]):
        for it in items:
            self.story.append(Paragraph(it, ST["bullet"], bulletText="•"))
        self.story.append(Spacer(1, 3))

    def note(self, text: str, tint=WARN):
        t = Table([[Paragraph(text, ST["note"])]], colWidths=[FRAME_W])
        t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), tint), ("LEFTPADDING", (0, 0), (-1, -1), 8),
                               ("RIGHTPADDING", (0, 0), (-1, -1), 8), ("TOPPADDING", (0, 0), (-1, -1), 6),
                               ("BOTTOMPADDING", (0, 0), (-1, -1), 6)]))
        self.story.extend([t, Spacer(1, 8)])

    def toc(self):
        toc = TableOfContents()
        toc.levelStyles = [ST["toc1"]]
        toc.dotsMinLevel = 0
        self.story.append(toc)

    def spacer(self, h_mm: float = 4):
        self.story.append(Spacer(1, h_mm * mm))

    def page_break(self):
        self.story.append(PageBreak())

    # blocks
    def tiles(self, items: Sequence[tuple]):
        """Row of stat tiles: (number, label)."""
        n = len(items)
        w = FRAME_W / n
        cells = [[Paragraph(num, ST["tile_n"]), Paragraph(lbl, ST["tile_l"])] for num, lbl in items]
        t = Table([[Table([[c[0]], [c[1]]], colWidths=[w - 10]) for c in cells]], colWidths=[w] * n)
        t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), TINT), ("LINEAFTER", (0, 0), (-2, -1), 2, colors.white),
                               ("TOPPADDING", (0, 0), (-1, -1), 6), ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
                               ("LEFTPADDING", (0, 0), (-1, -1), 6), ("VALIGN", (0, 0), (-1, -1), "TOP")]))
        self.story.extend([t, Spacer(1, 8)])

    def table(self, header: Sequence[str], rows: Sequence[Sequence], widths: Optional[Sequence[float]] = None,
              caption: str = "", zebra: bool = True, font_size: Optional[float] = None):
        cell = ST["cell"] if font_size is None else ParagraphStyle("c2", parent=ST["cell"], fontSize=font_size,
                                                                   leading=font_size * 1.27)
        cellb = ST["cellb"] if font_size is None else ParagraphStyle("c2b", parent=ST["cellb"], fontSize=font_size,
                                                                     leading=font_size * 1.27)
        data = [[Paragraph(str(h), cellb) for h in header]]
        data += [[Paragraph("" if v is None else str(v), cell) for v in r] for r in rows]
        widths = [w * FRAME_W for w in widths] if widths else None
        t = Table(data, colWidths=widths, repeatRows=1)
        style = [("LINEBELOW", (0, 0), (-1, 0), 0.8, INK3), ("LINEBELOW", (0, -1), (-1, -1), 0.5, RULE),
                 ("VALIGN", (0, 0), (-1, -1), "TOP"), ("TOPPADDING", (0, 0), (-1, -1), 2.5),
                 ("BOTTOMPADDING", (0, 0), (-1, -1), 2.5), ("LEFTPADDING", (0, 0), (-1, -1), 3),
                 ("RIGHTPADDING", (0, 0), (-1, -1), 3)]
        if zebra:
            style += [("BACKGROUND", (0, i), (-1, i), TINT) for i in range(2, len(data), 2)]
        t.setStyle(TableStyle(style))
        self.story.append(t)
        if caption:
            self.tab_no += 1
            self.story.append(Paragraph(f"<b>Table {self.tab_no}.</b> {caption}", ST["caption"]))
        else:
            self.story.append(Spacer(1, 8))

    def figure(self, path: Path, caption: str, width_frac: float = 1.0, max_h_frac: float = 0.82):
        with PILImage.open(path) as im:
            w_px, h_px = im.size
        w = FRAME_W * width_frac
        h = w * h_px / w_px
        max_h = FRAME_H * max_h_frac
        if h > max_h:
            w, h = w * max_h / h, max_h
        self.fig_no += 1
        img = Image(str(path), width=w, height=h)
        img.hAlign = "CENTER"
        self.story.append(KeepTogether([img, Paragraph(f"<b>Figure {self.fig_no}.</b> {caption}", ST["caption"])]))

    def build(self, path: Path, title: str, footer: str):
        doc = _Doc(path, title, footer)
        doc.multiBuild(self.story)
