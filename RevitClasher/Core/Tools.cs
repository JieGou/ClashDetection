﻿using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RevitClasher
{
    class Clash

    {
        public static void Execute(Document doc)
        {

            if (!MainUserControl._Reset)
            {

                var clashing = Clash.clashingElements(RevitTools.Doc, RevitTools.App);
                foreach (var item in clashing)
                {
                    MainUserControl.elementsClashingA.Add(item);
                }

                RevitTools.OverrideInView(clashing, RevitTools.Doc);
            }
            else
            {
                RevitTools.OverrideInView(new List<Element>(), RevitTools.Doc);

            }
        }

        public static SortedList<String, Document> Documents(Document doc, Application app)
        {
            SortedList<string, Document> output = new SortedList<string, Document>();

            foreach (Document d in app.Documents)
            {

                output.Add(d.Title, d);
            }
            return output;
        }
        public static List<ClashItems> clashingElements(Document doc, Application app, Document link = null)
        {
            //TODO : Create a separate addin to save configuration

            var linkedElements = new List<Element>();
            var localElements = new List<Element>();
            var ClashingElementsA = new List<Element>();
            var ClashingElementsB = new List<Element>();

            //If second document was not provided, use first doc
            var SecondDocument = FormTools.linkedDocument == null ? doc : FormTools.linkedDocument;

            //Get the transformation of the linked model from the project location
            var transform = GetTransform(doc, SecondDocument);

            Autodesk.Revit.DB.Options opt = new Options();
            var ActiveViewBB = doc.ActiveView as View3D;

            //Hard coded selection
            LogicalOrFilter filterLinkedCategories = new LogicalOrFilter(FormTools.SelectedCategories);
            FilteredElementCollector collector = new FilteredElementCollector(SecondDocument).WherePasses(filterLinkedCategories).WhereElementIsNotElementType();
            linkedElements = collector.ToElements() as List<Element>;

            var ClashItemsList = new List<ClashItems>();
            foreach (Element elementB in linkedElements)
            {

                GeometryElement geom = elementB.get_Geometry(opt);
                GeometryElement geomTranslated = geom;
                // Get bounding box from transformed geometry
                // By default use element geoemtry to extract bbox
                var bbox = geom.GetBoundingBox();
                if (transform != null)
                {
                    //If translation is valid, use it to override the bbox
                    geomTranslated = geom.GetTransformed(transform);
                    bbox = geomTranslated.GetBoundingBox();
                }

                if (belongsToView(ActiveViewBB, bbox))
                {
                    //VisibleLinkedElements.Add(geomTranslated);
                    Outline outline = new Outline(bbox.Min, bbox.Max);
                    BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outline);

                    LogicalOrFilter logicalOrFilter = new LogicalOrFilter(FormTools.SelectedHostCategories);

                    FilteredElementCollector bbClashingCollector = new FilteredElementCollector(doc, doc.ActiveView.Id).WherePasses(logicalOrFilter).WherePasses(bbFilter);

                    foreach (Element elementA in bbClashingCollector.ToElements())
                    {
                        if (!ClashingElementsA.Contains(elementA))
                        {
                            if (getClashWithSolid(doc, geomTranslated, elementA))
                            {
                                ClashItems clashItems = new ClashItems
                                {
                                    ElementA = elementA,
                                    ElementB = elementB
                                };

                                 ClashItemsList.Add(clashItems);
                            }
                        }
                    }
                }
            }

            return ClashItemsList;
        }

        /// <summary>
        /// Returns transform difference between documents
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="linked"></param>
        /// <returns></returns>
        public static Transform GetTransform(Document doc, Document linked)
        {

            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance));
            Transform output = null;
            foreach (var item in collector)
            {
                var rvtlink = item as RevitLinkInstance;
                if (rvtlink.Name.Contains(linked.Title))
                {

                    output = rvtlink.GetTransform();
                }

            }

            return output;
        }


        public static bool belongsToView(View3D view3D, BoundingBoxXYZ bb)
        {
            var SectionBox = view3D.GetSectionBox();
            var vMax = SectionBox.Max + SectionBox.Transform.Origin;
            var vMin = SectionBox.Min + SectionBox.Transform.Origin;
            var bbMax = bb.Max;
            var bbMin = bb.Min;

            if (bbMin.X < vMax.X && bbMax.X > vMin.X)
            {
                if (bbMin.Y < vMax.Y && bbMax.Y > vMin.Y)
                    if (bbMin.Z < vMax.Z && bbMax.Z > vMin.Z)
                    {
                        return true;
                    }
            }
            return false;
        }


        /// <summary>
        /// Detects the collision or clash between two elements. By analyzing the geometry intersection of an element (Element A) against other element (Element B)
        /// </summary>
        /// <param name="doc">Active revit document</param>
        /// <param name="geometryElement">Geometry element from which the intersection will be verified (Element B)</param>
        /// <param name="element">Element to verify the presence of intersection (Element A)</param>
        /// <returns>A Boolean flag that indicates whether or not there is a clash between elements. True = Clash detected. False = No clash detected</returns>
        public static bool getClashWithSolid(Document doc, GeometryElement geometryElement, Element element)
        {
            // Elements list, This list contains the Id's of element A
            List<ElementId> elementsList = new List<ElementId>();
            elementsList.Add(element.Id);

            bool clashOutputFlag = false;

            //Look for each solid in linked model
            foreach (GeometryObject geomObj in geometryElement)
            {
                // If solid is present we perform a solid intersection filter.
                // and later, perform a filter iteration with the element to clash (Element A vs Element B)
                if(geomObj is Solid)
                {
                    Solid solidObject = geomObj as Solid;
                    ElementIntersectsSolidFilter elementIntersectsSolidFilter = new ElementIntersectsSolidFilter(solidObject);
                    FilteredElementCollector collector = new FilteredElementCollector(doc, elementsList).WherePasses(elementIntersectsSolidFilter);
                    
                    // If collector count is zero, this means that Solid intersection can't be possible with the given element (ie: Fabrication Parts).
                    if(collector.Count() == 0)
                    {
                        BoundingBoxXYZ bbox = geometryElement.GetBoundingBox();
                        Outline outline = new Outline(bbox.Min, bbox.Max);
                        BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outline);
                        collector = new FilteredElementCollector(doc, elementsList).WherePasses(bbFilter);
                    }

                    foreach (var item in collector)
                    {
                        clashOutputFlag = true;
                        break;
                    }
                }

                // If a Geometry instance is present. We implement a BoundingBox intersect filter over the Geometry of element A
                // and later, perform a Filter iteration with the element to clash (Element A vs Element B)
                if(geomObj is GeometryInstance)
                {
                    BoundingBoxXYZ bbox = geometryElement.GetBoundingBox();
                    Outline outline = new Outline(bbox.Min, bbox.Max);
                    BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outline);
                    FilteredElementCollector collector = new FilteredElementCollector(doc, elementsList).WherePasses(bbFilter);
                    foreach (var item in collector)
                    {
                        clashOutputFlag = true;
                        break;
                    }
                }

                if (!clashOutputFlag) break;
            }

            return clashOutputFlag;
        }

        /// <summary>
        /// Selection A, first list, local elements
        /// </summary>
        /// <returns></returns>
        internal static List<ElementFilter> SelectionA()
        {
            //TODO: change to UI selection
            var categories = new List<ElementFilter>() {
                new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves),
                new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),

        };


            return categories;
        }

        /// <summary>
        /// Selection B, second list
        /// </summary>
        /// <returns></returns>
        internal static List<ElementFilter> SelectionB()
        {//TODO: change to UI selection
            var categories = new List<ElementFilter>() {
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),

        };


            return categories;
        }
    }

    /// <summary>
    /// Future implementation, groud by clashing elements
    /// </summary>
    public class ClashItems
    {
        //public static RevitElement ElementA { get; set; }

        //public static RevitElement ElementB { get; set; }

        public string Name
        {
            get { return "Group " + ElementA.Name + "-" + ElementB.Name; }
        }

        public Element ElementA { get; internal set; }

        public Element ElementB { get; internal set; }
        public string ToString()
        {
            return Name;
        }

    }

    class RevitTools
    {
        public static ExternalCommandData commandData { get; set; }
        public static Document Doc { get; set; }
        public static Application App { get; set; }
        public static UIDocument Uidoc { get; internal set; }

        /// <summary>
        /// Clear all overrides in view and just overrides selection to red
        /// </summary>
        /// <param name="ClashingElements"></param>
        /// <param name="doc"></param>
        public static void OverrideInView(List<Element> ClashingElements, Document doc)
        {
            var activeView = doc.ActiveView;

            ResetView();

            OverrideGraphicSettings ogsA = new OverrideGraphicSettings();

            foreach (var item in ClashingElements)
            {
                #if REVIT2020 || REVIT2019
                OverrideElemtColor.Graphics20192020(doc, ref ogsA, 255, 0, 0);
                #else
                    OverrideElemtColor.Graphics20172018(doc, ref ogsA, 255, 0, 0);
                #endif
                activeView.SetElementOverrides(item.Id, ogsA);
            }


            OverrideGraphicSettings ogsB = new OverrideGraphicSettings();

            foreach (var item in ClashingElements)
            {

                #if REVIT2020 || REVIT2019
                OverrideElemtColor.Graphics20192020(doc, ref ogsB, 0, 0, 255);
                #else
                OverrideElemtColor.Graphics20172018(doc, ref ogsA, 0, 0, 255);
                #endif

                activeView.SetElementOverrides(item.Id, ogsA);
            }

        }


        /// <summary>
        /// Clear all overrides in view and just overrides selection to red
        /// </summary>
        /// <param name="ClashingElements"></param>
        /// <param name="doc"></param>
        public static void OverrideInView(List<ClashItems> ClashingElements, Document doc)
        {
            var activeView = doc.ActiveView;

            ResetView();



            var c = new List<ElementId>();

            OverrideGraphicSettings ogsA = new OverrideGraphicSettings();
            ogsA.SetProjectionLineColor(new Color(255, 0, 0));
            foreach (var e in ClashingElements.Select(x => x.ElementA).ToList())
            {
                activeView.SetElementOverrides(e.Id, ogsA);
                c.Add(e.Id);
            }



            OverrideGraphicSettings ogsB = new OverrideGraphicSettings();
            ogsB.SetProjectionLineColor(new Color(0, 255, 0));
            foreach (var e in ClashingElements.Select(x => x.ElementB).ToList())
            {
                activeView.SetElementOverrides(e.Id, ogsB);
                c.Add(e.Id);
            }
        }



        public static void Focus(ClashItems elements)
        {
            if (MainUserControl._CropBox)
            {
                CreateBox(elements);
            }
            View activeView = RevitTools.Doc.ActiveView;
            ResetView();
            var listOfId = new List<ElementId>();
            listOfId.Add(elements.ElementA.Id);
            listOfId.Add(elements.ElementB.Id);
            RevitTools.Uidoc.Selection.SetElementIds(listOfId);
            RevitTools.Uidoc.ShowElements(listOfId);
        }

        public static void CreateBox(ClashItems elements)
        {
            View activeView = (View3D)RevitTools.Doc.ActiveView;
            if (MainUserControl._CropBox)
            {

                BoundingBoxXYZ boundingBoxXYZ = elements.ElementA.get_BoundingBox(activeView);
                activeView.CropBox = boundingBoxXYZ;

            }
            else
            {
                activeView.CropBox = null;
            }

        }

        public static void ResetView()
        {
            View activeView = RevitTools.Doc.ActiveView;

            FilteredElementCollector coll = new FilteredElementCollector(RevitTools.Doc, activeView.Id).WhereElementIsViewIndependent().WhereElementIsNotElementType();
            var output = coll.ToElementIds();
            OverrideGraphicSettings clean = new OverrideGraphicSettings();
            foreach (var element in output)
            {
                activeView.SetElementOverrides(element, clean);
            }



            if (activeView.IsTemporaryHideIsolateActive())
            {
                TemporaryViewMode tempView = TemporaryViewMode.TemporaryHideIsolate;
                activeView.DisableTemporaryViewMode(tempView);
            }

        }
    }
}
