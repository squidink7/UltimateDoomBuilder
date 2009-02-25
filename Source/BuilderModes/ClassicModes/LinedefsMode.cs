
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using CodeImp.DoomBuilder.Windows;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Config;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	[EditMode(DisplayName = "Linedefs Mode",
			  SwitchAction = "linedefsmode",	// Action name used to switch to this mode
			  ButtonImage = "LinesMode.png",	// Image resource name for the button
			  ButtonOrder = int.MinValue + 100,	// Position of the button (lower is more to the left)
			  ButtonGroup = "000_editing",
			  UseByDefault = true)]

	public class LinedefsMode : BaseClassicMode
	{
		#region ================== Constants

		public const float LINEDEF_HIGHLIGHT_RANGE = 20f;

		#endregion

		#region ================== Variables

		// Highlighted item
		private Linedef highlighted;
		private Association[] association = new Association[Linedef.NUM_ARGS];
		private Association highlightasso = new Association();
		
		// Interface
		private bool editpressed;
		
		#endregion

		#region ================== Properties

		#endregion

		#region ================== Constructor / Disposer

		#endregion

		#region ================== Methods

		// This highlights a new item
		protected void Highlight(Linedef l)
		{
			bool completeredraw = false;
			LinedefActionInfo action = null;

			// Often we can get away by simply undrawing the previous
			// highlight and drawing the new highlight. But if associations
			// are or were drawn we need to redraw the entire display.
			
			// Previous association highlights something?
			if((highlighted != null) && (highlighted.Tag > 0)) completeredraw = true;
			
			// Set highlight association
			if(l != null)
				highlightasso.Set(l.Tag, UniversalType.LinedefTag);
			else
				highlightasso.Set(0, 0);

			// New association highlights something?
			if((l != null) && (l.Tag > 0)) completeredraw = true;

			// Use the line tag to highlight sectors (Doom style)
			if(General.Map.Config.LineTagIndicatesSectors)
			{
				if(l != null)
					association[0].Set(l.Tag, UniversalType.SectorTag);
				else
					association[0].Set(0, 0);
			}
			else
			{
				if(l != null)
				{
					// Check if we can find the linedefs action
					if((l.Action > 0) && General.Map.Config.LinedefActions.ContainsKey(l.Action))
						action = General.Map.Config.LinedefActions[l.Action];
				}
				
				// Determine linedef associations
				for(int i = 0; i < Linedef.NUM_ARGS; i++)
				{
					// Previous association highlights something?
					if((association[i].type == UniversalType.SectorTag) ||
					   (association[i].type == UniversalType.LinedefTag) ||
					   (association[i].type == UniversalType.ThingTag)) completeredraw = true;

					// Make new association
					if(action != null)
						association[i].Set(l.Args[i], action.Args[i].Type);
					else
						association[i].Set(0, 0);

					// New association highlights something?
					if((association[i].type == UniversalType.SectorTag) ||
					   (association[i].type == UniversalType.LinedefTag) ||
					   (association[i].type == UniversalType.ThingTag)) completeredraw = true;
				}
			}
			
			// If we're changing associations, then we
			// need to redraw the entire display
			if(completeredraw)
			{
				// Set new highlight and redraw completely
				highlighted = l;
				General.Interface.RedrawDisplay();
			}
			else
			{
				// Update display
				if(renderer.StartPlotter(false))
				{
					// Undraw previous highlight
					if((highlighted != null) && !highlighted.IsDisposed)
					{
						renderer.PlotLinedef(highlighted, renderer.DetermineLinedefColor(highlighted));
						renderer.PlotVertex(highlighted.Start, renderer.DetermineVertexColor(highlighted.Start));
						renderer.PlotVertex(highlighted.End, renderer.DetermineVertexColor(highlighted.End));
					}

					// Set new highlight
					highlighted = l;

					// Render highlighted item
					if((highlighted != null) && !highlighted.IsDisposed)
					{
						renderer.PlotLinedef(highlighted, General.Colors.Highlight);
						renderer.PlotVertex(highlighted.Start, renderer.DetermineVertexColor(highlighted.Start));
						renderer.PlotVertex(highlighted.End, renderer.DetermineVertexColor(highlighted.End));
					}

					// Done
					renderer.Finish();
					renderer.Present();
				}
			}

			// Show highlight info
			if((highlighted != null) && !highlighted.IsDisposed)
				General.Interface.ShowLinedefInfo(highlighted);
			else
				General.Interface.HideInfo();
		}
		
		#endregion
		
		#region ================== Events

		// Cancel mode
		public override void OnCancel()
		{
			base.OnCancel();

			// Return to this mode
			General.Editing.ChangeMode(new LinedefsMode());
		}

		// Mode engages
		public override void OnEngage()
		{
			base.OnEngage();
			renderer.SetPresentation(Presentation.Standard);
			
			// Convert geometry selection to linedefs selection
			General.Map.Map.ClearAllMarks(false);
			General.Map.Map.MarkSelectedVertices(true, true);
			ICollection<Linedef> lines = General.Map.Map.LinedefsFromMarkedVertices(false, true, false);
			foreach(Linedef l in lines) l.Selected = true;
			General.Map.Map.ClearSelectedSectors();
			General.Map.Map.ClearSelectedVertices();
		}
		
		// Mode disengages
		public override void OnDisengage()
		{
			base.OnDisengage();

			// Going to EditSelectionMode?
			if(General.Editing.NewMode is EditSelectionMode)
			{
				// No selection made? But we have a highlight!
				if((General.Map.Map.GetSelectedLinedefs(true).Count == 0) && (highlighted != null))
				{
					// Make the highlight the selection
					highlighted.Selected = true;
				}
			}

			// Hide highlight info
			General.Interface.HideInfo();
		}

		// This redraws the display
		public override void OnRedrawDisplay()
		{
			renderer.RedrawSurface();

			// Render lines
			if(renderer.StartPlotter(true))
			{
				renderer.PlotLinedefSet(General.Map.Map.Linedefs);
				for(int i = 0; i < Linedef.NUM_ARGS; i++) BuilderPlug.Me.PlotAssociations(renderer, association[i]);
				if((highlighted != null) && !highlighted.IsDisposed)
				{
					BuilderPlug.Me.PlotReverseAssociations(renderer, highlightasso);
					renderer.PlotLinedef(highlighted, General.Colors.Highlight);
				}
				renderer.PlotVerticesSet(General.Map.Map.Vertices);
				renderer.Finish();
			}

			// Render things
			if(renderer.StartThings(true))
			{
				renderer.RenderThingSet(General.Map.ThingsFilter.HiddenThings, Presentation.THINGS_HIDDEN_ALPHA);
				renderer.RenderThingSet(General.Map.ThingsFilter.VisibleThings, 1.0f);
				renderer.Finish();
			}

			// Render selection
			if(renderer.StartOverlay(true))
			{
				for(int i = 0; i < Linedef.NUM_ARGS; i++) BuilderPlug.Me.RenderAssociations(renderer, association[i]);
				if((highlighted != null) && !highlighted.IsDisposed) BuilderPlug.Me.RenderReverseAssociations(renderer, highlightasso);
				if(selecting) RenderMultiSelection();
				renderer.Finish();
			}

			renderer.Present();
		}

		// Selection
		protected override void OnSelectBegin()
		{
			// Item highlighted?
			if((highlighted != null) && !highlighted.IsDisposed)
			{
				// Flip selection
				highlighted.Selected = !highlighted.Selected;

				// Update display
				if(renderer.StartPlotter(false))
				{
					// Redraw highlight to show selection
					renderer.PlotLinedef(highlighted, renderer.DetermineLinedefColor(highlighted));
					renderer.PlotVertex(highlighted.Start, renderer.DetermineVertexColor(highlighted.Start));
					renderer.PlotVertex(highlighted.End, renderer.DetermineVertexColor(highlighted.End));
					renderer.Finish();
					renderer.Present();
				}
			}
			else
			{
				// Start rectangular selection
				StartMultiSelection();
			}

			base.OnSelectBegin();
		}

		// End selection
		protected override void OnSelectEnd()
		{
			// Not stopping from multiselection?
			if(!selecting)
			{
				// Item highlighted?
				if((highlighted != null) && !highlighted.IsDisposed)
				{
					// Update display
					if(renderer.StartPlotter(false))
					{
						// Render highlighted item
						renderer.PlotLinedef(highlighted, General.Colors.Highlight);
						renderer.PlotVertex(highlighted.Start, renderer.DetermineVertexColor(highlighted.Start));
						renderer.PlotVertex(highlighted.End, renderer.DetermineVertexColor(highlighted.End));
						renderer.Finish();
						renderer.Present();
					}
				}
			}

			base.OnSelectEnd();
		}
		
		// Start editing
		protected override void OnEditBegin()
		{
			// Item highlighted?
			if((highlighted != null) && !highlighted.IsDisposed)
			{
				// Edit pressed in this mode
				editpressed = true;

				// Highlighted item not selected?
				if(!highlighted.Selected)
				{
					// Make this the only selection
					General.Map.Map.ClearSelectedLinedefs();
					highlighted.Selected = true;
					General.Interface.RedrawDisplay();
				}

				// Update display
				if(renderer.StartPlotter(false))
				{
					// Redraw highlight to show selection
					renderer.PlotLinedef(highlighted, renderer.DetermineLinedefColor(highlighted));
					renderer.PlotVertex(highlighted.Start, renderer.DetermineVertexColor(highlighted.Start));
					renderer.PlotVertex(highlighted.End, renderer.DetermineVertexColor(highlighted.End));
					renderer.Finish();
					renderer.Present();
				}
			}
			else
			{
				// Start drawing mode
				DrawGeometryMode drawmode = new DrawGeometryMode();
				bool snaptogrid = General.Interface.ShiftState ^ General.Interface.SnapToGrid;
				bool snaptonearest = General.Interface.CtrlState ^ General.Interface.AutoMerge;
				DrawnVertex v = DrawGeometryMode.GetCurrentPosition(mousemappos, snaptonearest, snaptogrid, renderer, new List<DrawnVertex>());
				drawmode.DrawPointAt(v);
				General.Editing.ChangeMode(drawmode);
			}
			
			base.OnEditBegin();
		}
		
		// Done editing
		protected override void OnEditEnd()
		{
			// Edit pressed in this mode?
			if(editpressed)
			{
				// Anything selected?
				ICollection<Linedef> selected = General.Map.Map.GetSelectedLinedefs(true);
				if(selected.Count > 0)
				{
					if(General.Interface.IsActiveWindow)
					{
						// Show line edit dialog
						General.Interface.ShowEditLinedefs(selected);

						// When a single line was selected, deselect it now
						if(selected.Count == 1) General.Map.Map.ClearSelectedLinedefs();

						// Update entire display
						General.Interface.RedrawDisplay();
					}
				}
			}

			editpressed = false;
			base.OnEditEnd();
		}
		
		// Mouse moves
		public override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			// Not holding any buttons?
			if(e.Button == MouseButtons.None)
			{
				// Find the nearest linedef within highlight range
				Linedef l = General.Map.Map.NearestLinedefRange(mousemappos, LINEDEF_HIGHLIGHT_RANGE / renderer.Scale);

				// Highlight if not the same
				if(l != highlighted) Highlight(l);
			}
		}

		// Mouse leaves
		public override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);

			// Highlight nothing
			Highlight(null);
		}

		// Mouse wants to drag
		protected override void OnDragStart(MouseEventArgs e)
		{
			base.OnDragStart(e);

			// Edit button used?
			if(General.Actions.CheckActionActive(null, "classicedit"))
			{
				// Anything highlighted?
				if((highlighted != null) && !highlighted.IsDisposed)
				{
					// Highlighted item not selected?
					if(!highlighted.Selected)
					{
						// Select only this linedef for dragging
						General.Map.Map.ClearSelectedLinedefs();
						highlighted.Selected = true;
					}

					// Start dragging the selection
					General.Editing.ChangeMode(new DragLinedefsMode(mousedownmappos));
				}
			}
		}

		// This is called wheh selection ends
		protected override void OnEndMultiSelection()
		{
			// Go for all lines
			foreach(Linedef l in General.Map.Map.Linedefs)
			{
				l.Selected = ((l.Start.Position.x >= selectionrect.Left) &&
							  (l.Start.Position.y >= selectionrect.Top) &&
							  (l.Start.Position.x <= selectionrect.Right) &&
							  (l.Start.Position.y <= selectionrect.Bottom) &&
							  (l.End.Position.x >= selectionrect.Left) &&
							  (l.End.Position.y >= selectionrect.Top) &&
							  (l.End.Position.x <= selectionrect.Right) &&
							  (l.End.Position.y <= selectionrect.Bottom));
			}

			base.OnEndMultiSelection();

			// Clear overlay
			if(renderer.StartOverlay(true)) renderer.Finish();

			// Redraw
			General.Interface.RedrawDisplay();
		}

		// This is called when the selection is updated
		protected override void OnUpdateMultiSelection()
		{
			base.OnUpdateMultiSelection();

			// Render selection
			if(renderer.StartOverlay(true))
			{
				RenderMultiSelection();
				renderer.Finish();
				renderer.Present();
			}
		}

		// When copying
		public override bool OnCopyBegin()
		{
			// No selection made? But we have a highlight!
			if((General.Map.Map.GetSelectedSectors(true).Count == 0) && (highlighted != null))
			{
				// Make the highlight the selection
				highlighted.Selected = true;
			}

			return base.OnCopyBegin();
		}

		// When pasting
		public override bool OnPasteBegin()
		{
			// No selection made? But we have a highlight!
			if((General.Map.Map.GetSelectedSectors(true).Count == 0) && (highlighted != null))
			{
				// Make the highlight the selection
				highlighted.Selected = true;
			}

			return base.OnPasteBegin();
		}
		
		#endregion

		#region ================== Actions

		// This clears the selection
		[BeginAction("clearselection", BaseAction = true)]
		public void ClearSelection()
		{
			// Clear selection
			General.Map.Map.ClearAllSelected();

			// Redraw
			General.Interface.RedrawDisplay();
		}
		
		// This creates a new vertex at the mouse position
		[BeginAction("insertitem", BaseAction = true)]
		public virtual void InsertVertexAction()
		{
			// Start drawing mode
			DrawGeometryMode drawmode = new DrawGeometryMode();
			if(mouseinside)
			{
				bool snaptogrid = General.Interface.ShiftState ^ General.Interface.SnapToGrid;
				bool snaptonearest = General.Interface.CtrlState ^ General.Interface.AutoMerge;
				DrawnVertex v = DrawGeometryMode.GetCurrentPosition(mousemappos, snaptonearest, snaptogrid, renderer, new List<DrawnVertex>());
				drawmode.DrawPointAt(v);
			}
			General.Editing.ChangeMode(drawmode);
		}
		
		[BeginAction("deleteitem", BaseAction = true)]
		public void DeleteItem()
		{
			// Make list of selected linedefs
			ICollection<Linedef> selected = General.Map.Map.GetSelectedLinedefs(true);
			if((selected.Count == 0) && (highlighted != null) && !highlighted.IsDisposed) selected.Add(highlighted);

			// Anything to do?
			if(selected.Count > 0)
			{
				// Make undo
				if(selected.Count > 1)
					General.Map.UndoRedo.CreateUndo("Delete " + selected.Count + " linedefs");
				else
					General.Map.UndoRedo.CreateUndo("Delete linedef");
				
				// Dispose selected linedefs
				foreach(Linedef ld in selected) ld.Dispose();
				
				// Update cache values
				General.Map.IsChanged = true;
				General.Map.Map.Update();

				// Invoke a new mousemove so that the highlighted item updates
				MouseEventArgs e = new MouseEventArgs(MouseButtons.None, 0, (int)mousepos.x, (int)mousepos.y, 0);
				OnMouseMove(e);
				
				// Redraw screen
				General.Interface.RedrawDisplay();
			}
		}
		
		[BeginAction("splitlinedefs")]
		public void SplitLinedefs()
		{
			// Make list of selected linedefs
			ICollection<Linedef> selected = General.Map.Map.GetSelectedLinedefs(true);
			if((selected.Count == 0) && (highlighted != null) && !highlighted.IsDisposed) selected.Add(highlighted);

			// Anything to do?
			if(selected.Count > 0)
			{
				// Make undo
				if(selected.Count > 1)
					General.Map.UndoRedo.CreateUndo("Split " + selected.Count + " linedefs");
				else
					General.Map.UndoRedo.CreateUndo("Split linedef");
				
				// Go for all linedefs to split
				foreach(Linedef ld in selected)
				{
					Vertex splitvertex;

					// Linedef highlighted?
					if(ld == highlighted)
					{
						// Split at nearest position on the line
						Vector2D nearestpos = ld.NearestOnLine(mousemappos);
						splitvertex = General.Map.Map.CreateVertex(nearestpos);
					}
					else
					{
						// Split in middle of line
						splitvertex = General.Map.Map.CreateVertex(ld.GetCenterPoint());
					}
					
					// Snap to map format accuracy
					splitvertex.SnapToAccuracy();
					
					// Split the line
					ld.Split(splitvertex);
				}

				// Update cache values
				General.Map.IsChanged = true;
				General.Map.Map.Update();
				
				// Redraw screen
				General.Interface.RedrawDisplay();
			}
		}
		
		[BeginAction("curvelinesmode")]
		public void CurveLinedefs()
		{
			// No selected lines?
			ICollection<Linedef> selected = General.Map.Map.GetSelectedLinedefs(true);
			if(selected.Count == 0)
			{
				// Anything highlighted?
				if(highlighted != null)
				{
					// Select the highlighted item
					highlighted.Selected = true;
					selected.Add(highlighted);
				}
			}

			// Any selected lines?
			if(selected.Count > 0)
			{
				// Go into curve linedefs mode
				General.Editing.ChangeMode(new CurveLinedefsMode(new LinedefsMode()));
			}
		}
		
		[BeginAction("fliplinedefs")]
		public void FlipLinedefs()
		{
			// No selected lines?
			ICollection<Linedef> selected = General.Map.Map.GetSelectedLinedefs(true);
			if(selected.Count == 0)
			{
				// Anything highlighted?
				if(highlighted != null)
				{
					// Select the highlighted item
					highlighted.Selected = true;
					selected.Add(highlighted);
				}
			}

			// Any selected lines?
			if(selected.Count > 0)
			{
				// Make undo
				if(selected.Count > 1)
					General.Map.UndoRedo.CreateUndo("Flip " + selected.Count + " linedefs");
				else
					General.Map.UndoRedo.CreateUndo("Flip linedef");

				// Flip all selected linedefs
				foreach(Linedef l in selected)
				{
					l.FlipVertices();
					l.FlipSidedefs();
				}
				
				// Remove selection if only one was selected
				if(selected.Count == 1)
				{
					foreach(Linedef ld in selected) ld.Selected = false;
					selected.Clear();
				}

				// Redraw
				General.Map.IsChanged = true;
				General.Interface.RefreshInfo();
				General.Interface.RedrawDisplay();
			}
		}

		[BeginAction("flipsidedefs")]
		public void FlipSidedefs()
		{
			// No selected lines?
			ICollection<Linedef> selected = General.Map.Map.GetSelectedLinedefs(true);
			if(selected.Count == 0)
			{
				// Anything highlighted?
				if(highlighted != null)
				{
					// Select the highlighted item
					highlighted.Selected = true;
					selected.Add(highlighted);
				}
			}

			// Any selected lines?
			if(selected.Count > 0)
			{
				// Make undo
				if(selected.Count > 1)
					General.Map.UndoRedo.CreateUndo("Flip " + selected.Count + " sidedefs");
				else
					General.Map.UndoRedo.CreateUndo("Flip sidedef");

				// Flip sidedefs in all selected linedefs
				foreach(Linedef l in selected)
				{
					l.FlipSidedefs();
					if(l.Front != null) l.Front.Sector.UpdateNeeded = true;
					if(l.Back != null) l.Back.Sector.UpdateNeeded = true;
				}

				// Remove selection if only one was selected
				if(selected.Count == 1)
				{
					foreach(Linedef ld in selected) ld.Selected = false;
					selected.Clear();
				}

				// Redraw
				General.Map.Map.Update();
				General.Map.IsChanged = true;
				General.Interface.RefreshInfo();
				General.Interface.RedrawDisplay();
			}
		}

		#endregion
	}
}
