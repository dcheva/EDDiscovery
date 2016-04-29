﻿using EDDiscovery;
using EDDiscovery.DB;
using EDDiscovery2.DB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using EDDiscovery2.Trilateration;
using OpenTK;

namespace EDDiscovery2._3DMap
{
    public class DatasetBuilder
    {
        private List<IData3DSet> _datasets;
        private static Dictionary<string, TexturedQuadData> _cachedTextures = new Dictionary<string, TexturedQuadData>();
        private static Dictionary<string, Data3DSetClass<TexturedQuadData>> _cachedCoordTextures = new Dictionary<string, Data3DSetClass<TexturedQuadData>>();

        public ISystem CenterSystem { get; set; } = new SystemClass();
        public ISystem SelectedSystem { get; set; } = new SystemClass();
        public List<ISystem> StarList { get; set; } = new List<ISystem>();
        public List<ISystem> ReferenceSystems { get; set; } = new List<ISystem>();
        public List<SystemPosition> VisitedSystems { get; set; }
        public List<ISystem> PlannedRoute { get; set; } = new List<ISystem>();

        public bool GridLines { get; set; } = false;
        public bool FineGridLines { get; set; } = false;
        public bool GridCoords { get; set; } = false;
        public bool DrawLines { get; set; } = false;
        public bool AllSystems { get; set; } = false;
        public bool Stations { get; set; } = false;
        public bool UseImage { get; set; } = false;

        public FGEImage[] Images { get; set; } = null;

        public Vector2 MinGridPos { get; set; } = new Vector2(-50000.0f, -20000.0f);
        public Vector2 MaxGridPos { get; set; } = new Vector2(50000.0f, 80000.0f);

        int gridunitSize = 1000;

        public DatasetBuilder()
        {
        }

        public List<IData3DSet> BuildMaps()
        {
            _datasets = new List<IData3DSet>();
            AddMapImages();
            return _datasets;
        }

        public List<IData3DSet> BuildGridLines()
        {
            _datasets = new List<IData3DSet>();
            AddGridLines();
            return _datasets;
        }

        public List<IData3DSet> BuildGridCoords()
        {
            _datasets = new List<IData3DSet>();
            AddGridCoords();
            return _datasets;
        }

        public List<IData3DSet> BuildStars()
        {
            _datasets = new List<IData3DSet>();
            AddStandardSystems();
            AddStations();
            AddPOIsToDataset();
            return _datasets;
        }

        public List<IData3DSet> BuildVisitedSystems()
        {
            _datasets = new List<IData3DSet>();
            AddVisitedSystemsInformation();
            AddRoutePlannerInfoToDataset();
            AddTrilaterationInfoToDataset();
            return _datasets;
        }

        public List<IData3DSet> BuildSelected()
        {
            _datasets = new List<IData3DSet>();
            AddCenterPointToDataset();
            AddSelectedSystemToDataset();
            return _datasets;
        }

        public void AddMapImages()
        {
            if (UseImage && Images != null && Images.Length != 0)
            {
                var datasetMapImg = Data3DSetClass<TexturedQuadData>.Create("mapimage", Color.White, 1.0f);
                foreach (var img in Images)
                {
                    if (_cachedTextures.ContainsKey(img.FileName))
                    {
                        datasetMapImg.Add(_cachedTextures[img.FileName]);
                    }
                    else
                    {
                        var texture = TexturedQuadData.FromFGEImage(img);
                        _cachedTextures[img.FileName] = texture;
                        datasetMapImg.Add(texture);
                    }
                }
                _datasets.Add(datasetMapImg);
            }
        }

        public Bitmap DrawGridBitmap(Bitmap text_bmp, float x, float z, Font fnt, int px, int py)
        {
            using (Graphics g = Graphics.FromImage(text_bmp))
            {
                //using (Brush br = new SolidBrush(Color.Yellow))
                // g.FillRectangle(br, 0, 0, text_bmp.Width, text_bmp.Height);

                using (Brush br = new SolidBrush((Color)System.Drawing.ColorTranslator.FromHtml("#296A6C")))
                    g.DrawString(x.ToString("0") + "," + z.ToString("0"), fnt, br, new Point(px, py));
            }

            return text_bmp;
        }

        public void AddGridCoords()
        {
            if (GridCoords)
            {
                string fontname = "MS Sans Serif";

                if (_cachedCoordTextures.ContainsKey(fontname))
                {
                    _datasets.Add(_cachedCoordTextures[fontname]);
                }
                else
                {
                    Font fnt = new Font(fontname, 20F);

                    int bitmapwidth, bitmapheight;
                    Bitmap text_bmp = new Bitmap(100, 30);
                    using (Graphics g = Graphics.FromImage(text_bmp))
                    {
                        SizeF sz = g.MeasureString("-99999,-99999", fnt);
                        bitmapwidth = (int)sz.Width + 4;
                        bitmapheight = (int)sz.Height + 4;
                    }
                    var datasetMapImg = Data3DSetClass<TexturedQuadData>.Create("text bitmap", Color.White, 1.0f);

                    int textheightly = 50;
                    int textwidthly = textheightly * bitmapwidth / bitmapheight;

                    int gridwide = (int)Math.Floor((MaxGridPos.X - MinGridPos.X) / gridunitSize + 1);
                    int gridhigh = (int)Math.Floor((MaxGridPos.Y - MinGridPos.Y) / gridunitSize + 1);
                    int texwide = 1024 / bitmapwidth;
                    int texhigh = 1024 / bitmapheight;
                    int numtex = (int)Math.Ceiling((gridwide * gridhigh) * 1.0 / (texwide * texhigh));
                    List<TexturedQuadData> basetextures = Enumerable.Range(0, numtex).Select(i => new TexturedQuadData(null, null, new Bitmap(1024, 1024))).ToList();

                    for (float x = MinGridPos.X; x <= MaxGridPos.X; x += gridunitSize)
                    {
                        for (float z = MinGridPos.Y; z <= MaxGridPos.Y; z += gridunitSize)
                        {
                            int num = (int)(Math.Floor((x - MinGridPos.X) / gridunitSize) * gridwide + Math.Floor((z - MinGridPos.Y) / gridunitSize));
                            int tex_x = (num % texwide) * bitmapwidth;
                            int tex_y = ((num / texwide) % texhigh) * bitmapheight;
                            int tex_n = num / (texwide * texhigh);

                            DrawGridBitmap(basetextures[tex_n].Texture, x, z, fnt, tex_x, tex_y);
                            datasetMapImg.Add(basetextures[tex_n].CreateSubTexture(
                                new Point((int)x, (int)z), new Point((int)x + textwidthly, (int)z),
                                new Point((int)x, (int)z + textheightly), new Point((int)x + textwidthly, (int)z + textheightly),
                                new Point(tex_x, tex_y + bitmapheight), new Point(tex_x + bitmapwidth, tex_y + bitmapheight),
                                new Point(tex_x, tex_y), new Point(tex_x + bitmapwidth, tex_y)));
                        }
                    }

                    _cachedCoordTextures[fontname] = datasetMapImg;

                    _datasets.Add(datasetMapImg);
                }
            }
        }

        public void AddGridLines()
        {
            int unitSize = 1000;

            if (FineGridLines)
            {
                int smallUnitSize = gridunitSize / 10;
                var smalldatasetGrid = Data3DSetClass<LineData>.Create("grid", (Color)System.Drawing.ColorTranslator.FromHtml("#202020"), 0.6f);

                int ratio = unitSize / smallUnitSize;
                int c = 0;

                for (float x = MinGridPos.X; x <= MaxGridPos.X; x += smallUnitSize)
                {
                    if (!GridLines || (c++ % ratio != 0))
                        smalldatasetGrid.Add(new LineData(x, 0, MinGridPos.Y, x, 0, MaxGridPos.Y));
                }
                c = 0;
                for (float z = MinGridPos.Y; z <= MaxGridPos.Y; z += smallUnitSize)
                {
                    if (!GridLines || (c++ % ratio != 0))
                        smalldatasetGrid.Add(new LineData(MinGridPos.X, 0, z, MaxGridPos.X, 0, z));
                }

                _datasets.Add(smalldatasetGrid);
            }

            if (GridLines)
            {
                var datasetGrid = Data3DSetClass<LineData>.Create("grid", (Color)System.Drawing.ColorTranslator.FromHtml("#296A6C"), 0.6f);

                for (float x = MinGridPos.X; x <= MaxGridPos.X; x += unitSize)
                {
                    datasetGrid.Add(new LineData(x, 0, MinGridPos.Y, x, 0, MaxGridPos.Y));
                }

                for (float z = MinGridPos.Y; z <= MaxGridPos.Y; z += unitSize)
                {
                    datasetGrid.Add(new LineData(MinGridPos.X, 0, z, MaxGridPos.X, 0, z));
                }

                _datasets.Add(datasetGrid);
            }
        }

        public void AddStandardSystems()
        {
            if (AllSystems && StarList != null)
            {
                bool addstations = !Stations;
                var datasetS = Data3DSetClass<PointData>.Create("stars", Color.White, 1.0f);

                foreach (ISystem si in StarList)
                {
                    if (addstations || si.population == 0)
                        AddSystem(si, datasetS);
                }
                _datasets.Add(datasetS);
            }
        }

        public void UpdateStandardSystems(ref List<IData3DSet> _datasets , DateTime maxtime)     // modify this dataset
        {

            var ds = from dataset in _datasets where dataset.Name.Equals("stars") select dataset;
            Data3DSetClass<PointData> datasetS = (Data3DSetClass<PointData>)ds.First();

            _datasets.Remove(datasetS);

           datasetS = Data3DSetClass<PointData>.Create("stars", Color.White, 1.0f);

            if (AllSystems && StarList != null)
            {
                bool addstations = !Stations;
         
                foreach (ISystem si in StarList)
                {
                    if (addstations || (si.population == 0 && si.CreateDate<maxtime))
                        AddSystem(si, datasetS);
                }
                _datasets.Add(datasetS);
            }
        }


        public void AddStations()
        {
            if (Stations)
            {
                var datasetS = Data3DSetClass<PointData>.Create("stations", Color.RoyalBlue, 1.0f);

                foreach (ISystem si in StarList)
                {
                    if (si.population > 0)
                        AddSystem(si, datasetS);
                }
                _datasets.Add(datasetS);
            }
        }

        public void AddVisitedSystemsInformation()
        {
            if (VisitedSystems != null && VisitedSystems.Any())
            {
                ISystem lastknownps = LastKnownSystemPosition();

                // For some reason I am unable to fathom this errors during the session after DBUpgrade8
                // colours just resolves to an object reference not set error, but after a restart it works fine
                // Not going to waste any more time, a one time restart is hardly the worst workaround in the world...
                IEnumerable<IGrouping<int, SystemPosition>> colours =
                    from SystemPosition sysPos in VisitedSystems where sysPos.vs!=null
                    group sysPos by sysPos.vs.MapColour;

                if (colours!=null)
                {
                    foreach (IGrouping<int, SystemPosition> colour in colours)
                    {
                        if (DrawLines)
                        {
                            var datasetl = Data3DSetClass<LineData>.Create("visitedstars" + colour.Key.ToString(), Color.FromArgb(colour.Key), 2.0f);
                            foreach (SystemPosition sp in colour)
                            {
                                if (sp.curSystem != null && sp.curSystem.HasCoordinate && sp.lastKnownSystem != null && sp.lastKnownSystem.HasCoordinate)
                                {
                                    datasetl.Add(new LineData(sp.curSystem.x, sp.curSystem.y, sp.curSystem.z,
                                        sp.lastKnownSystem.x, sp.lastKnownSystem.y, sp.lastKnownSystem.z));

                                }
                            }
                            _datasets.Add(datasetl);
                        }
                        else
                        {
                            var datasetvs = Data3DSetClass<PointData>.Create("visitedstars" + colour.Key.ToString(), Color.FromArgb(colour.Key), 2.0f);
                            foreach (SystemPosition sp in colour)
                            {
                                ISystem star = SystemData.GetSystem(sp.Name);
                                if (star != null && star.HasCoordinate)
                                {

                                    AddSystem(star, datasetvs);
                                }
                            }
                            _datasets.Add(datasetvs);
                        }

                    }
                }
            }
        }

        // Planned change: Centered system will be marked but won't be "center" of the galaxy
        // dataset anymore. The origin will stay at Sol.
        public void AddCenterPointToDataset()
        {
            var dataset = Data3DSetClass<PointData>.Create("Center", Color.Yellow, 5.0f);

            //GL.Enable(EnableCap.ProgramPointSize);
            dataset.Add(new PointData(CenterSystem.x, CenterSystem.y, CenterSystem.z));
            _datasets.Add(dataset);
        }

        public void AddSelectedSystemToDataset()
        {
            if (SelectedSystem != null)
            {
                var dataset = Data3DSetClass<PointData>.Create("Selected", Color.Orange, 5.0f);

                //GL.Enable(EnableCap.ProgramPointSize);
                dataset.Add(new PointData(SelectedSystem.x, SelectedSystem.y, SelectedSystem.z));
                _datasets.Add(dataset);
            }
        }

        public void AddPOIsToDataset()
        {
            var dataset = Data3DSetClass<PointData>.Create("Interest", Color.Purple, 10.0f);
            AddSystem("sol", dataset);
            AddSystem("sagittarius a*", dataset);
            //AddSystem("polaris", dataset);
            _datasets.Add(dataset);
        }

        public void AddTrilaterationInfoToDataset()
        {
            if (ReferenceSystems != null && ReferenceSystems.Any())
            {
                var referenceLines = Data3DSetClass<LineData>.Create("CurrentReference", Color.Green, 5.0f);
                foreach (var refSystem in ReferenceSystems)
                {
                    referenceLines.Add(new LineData(CenterSystem.x, CenterSystem.y, CenterSystem.z, refSystem.x, refSystem.y, refSystem.z));
                }

                _datasets.Add(referenceLines);

                var lineSet = Data3DSetClass<LineData>.Create("SuggestedReference", Color.DarkOrange, 5.0f);


                Stopwatch sw = new Stopwatch();
                sw.Start();
                SuggestedReferences references = new SuggestedReferences(CenterSystem.x, CenterSystem.y, CenterSystem.z);

                for (int ii = 0; ii < 16; ii++)
                {
                    var rsys = references.GetCandidate();
                    if (rsys == null) break;
                    var system = rsys.System;
                    references.AddReferenceStar(system);
                    if (ReferenceSystems != null && ReferenceSystems.Any(s => s.name == system.name)) continue;
                    System.Diagnostics.Trace.WriteLine(string.Format("{0} Dist: {1} x:{2} y:{3} z:{4}", system.name, rsys.Distance.ToString("0.00"), system.x, system.y, system.z));
                    lineSet.Add(new LineData(CenterSystem.x, CenterSystem.y, CenterSystem.z, system.x, system.y, system.z));
                }
                sw.Stop();
                System.Diagnostics.Trace.WriteLine("Reference stars time " + sw.Elapsed.TotalSeconds.ToString("0.000s"));
                _datasets.Add(lineSet);
            }
        }

        public void AddRoutePlannerInfoToDataset()
        {
            if (PlannedRoute != null && PlannedRoute.Any())
            {
                var routeLines = Data3DSetClass<LineData>.Create("PlannedRoute", Color.DarkOrange, 25.0f);
                ISystem prevSystem = PlannedRoute.First();
                foreach (ISystem point in PlannedRoute.Skip(1))
                {
                    routeLines.Add(new LineData(prevSystem.x, prevSystem.y, prevSystem.z, point.x, point.y, point.z));
                    prevSystem = point;
                }
                _datasets.Add(routeLines);
            }
        }

        private void AddSystem(string systemName, Data3DSetClass<PointData> dataset)
        {
            AddSystem(SystemData.GetSystem(systemName), dataset);
        }

        private void AddSystem(ISystem system, Data3DSetClass<PointData> dataset)
        {
            if (system != null && system.HasCoordinate)
            {
                dataset.Add(new PointData(system.x, system.y, system.z));
            }
        }

        private ISystem LastKnownSystemPosition()
        {
            ISystem lastknownps = null;
            foreach (SystemPosition ps in VisitedSystems)
            {
                if (ps.curSystem == null)
                {
                    ps.curSystem = SystemData.GetSystem(ps.Name);
                }

                if (ps.curSystem != null && ps.curSystem.HasCoordinate)
                {
                    ps.lastKnownSystem = lastknownps;
                    lastknownps = ps.curSystem;
                }
            }
            return lastknownps;
        }

    }
}
