"""matplotlib's backend in the FryPDF notebook: figures become PNG output of the cell, as Jupyter's inline backend
does. plt.show() shows every open figure; figures a cell leaves open are shown when it ends. Selected by the kernel as
"module://fry_matplotlib" when matplotlib is imported; matplotlib imports this module only then."""

from matplotlib._pylab_helpers import Gcf
from matplotlib.backend_bases import FigureManagerBase, _Backend
from matplotlib.backends.backend_agg import FigureCanvasAgg

import fry_display


def flush_figures():
    """Shows every open figure in the cell's output, then closes them."""
    managers = list(Gcf.get_all_fig_managers())
    if not managers:
        return
    for manager in managers:
        data, metadata = fry_display.figure_bundle(manager.canvas.figure)
        if fry_display._publisher is not None:
            fry_display._publisher(data, metadata)
    import matplotlib.pyplot as plt
    plt.close("all")


class FigureManagerFry(FigureManagerBase):
    def show(self):
        data, metadata = fry_display.figure_bundle(self.canvas.figure, close=True)
        if fry_display._publisher is not None:
            fry_display._publisher(data, metadata)


class FigureCanvasFry(FigureCanvasAgg):
    manager_class = FigureManagerFry


@_Backend.export
class _BackendFry(_Backend):
    FigureCanvas = FigureCanvasFry
    FigureManager = FigureManagerFry

    @staticmethod
    def show(*args, **kwargs):
        flush_figures()
