﻿using EDDiscovery;
using EDDiscovery.Actions;
using EDDiscovery.DB;
using EDDiscovery2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EDDiscovery2
{
    public partial class JSONFiltersForm : Form
    {
        public JSONFilter result;
        EDDiscovery.Actions.ActionProgramList actionprogs;
        List<string> eventlist;
        List<string> fieldnames; // null means look up dynamically

        class Group
        {
            public Control panel;
            public string[] fieldnames;
            public ExtendedControls.ButtonExt upbutton;
            public ExtendedControls.ComboBoxCustom evlist;
            public ExtendedControls.ComboBoxCustom actionlist;
            public ExtendedControls.ButtonExt actionconfig;
            public ExtendedControls.ComboBoxCustom innercond;
            public ExtendedControls.ComboBoxCustom outercond;
            public Label outerlabel;

            public string actiondata;
        };

        List<Group> groups;
        bool allowoutercond;
        public int panelwidth;
        public int condxoffset;
        EDDiscovery2.EDDTheme theme;
        const int vscrollmargin = 10;
        const int panelmargin = 3;
        const int conditionhoff = 30;

        public JSONFiltersForm()
        {
            InitializeComponent();
            groups = new List<Group>();
        }

        public void InitFilter(string t,  EDDiscovery2.EDDTheme th, JSONFilter j = null)
        {
            List<string> events = EDDiscovery.EliteDangerous.JournalEntry.GetListOfEventsWithOptMethod(false);
            events.Add("All");
            Init(t, events, null, null, true, th, j);
        }

        public void InitAction(string t, EDDiscovery.Actions.ActionProgramList aclist, EDDiscovery2.EDDTheme th, JSONFilter j = null)
        {
            List<string> events = EDDiscovery.EliteDangerous.JournalEntry.GetListOfEventsWithOptMethod(false);
            events.Add("All");
            Init(t, events, null, aclist, false, th, j);
        }

        public void InitCondition(string t, List<string> fields, EDDiscovery2.EDDTheme th, JSONFilter j = null)
        {
            Init(t, null, fields, null, true, th, j);
        }

        public void Init(string t, List<string> el,                             // list of event types or null if event types not used
                                   List<string> flist,                          // list of field names, or null for lookup
                                   EDDiscovery.Actions.ActionProgramList aclist,// if aclist is null no action list
                                   bool outerconditions,
                                   EDDiscovery2.EDDTheme th, JSONFilter j = null)  
        {                                                                        // outc selects if group outer action can be selected, else its OR
            theme = th;
            eventlist = el;
            actionprogs = aclist;
            fieldnames = flist;
            allowoutercond = outerconditions;
            result = j;

            condxoffset = ((eventlist!= null) ? 148 : 0) + ((actionprogs!= null) ? (140+8+24+8) : 0) + panelmargin;
            panelwidth = condxoffset + 620;

            bool winborder = theme.ApplyToForm(this, SystemFonts.DefaultFont);
            statusStripCustom.Visible = panelTop.Visible = panelTop.Enabled = !winborder;
            this.Text = label_index.Text = t;

            if (result != null)
            {
                foreach (JSONFilter.FilterEvent fe in result.filters)
                {
                    Group g = CreateGroup(fe.eventname, fe.action, fe.actiondata , fe.innercondition.ToString(), fe.outercondition.ToString());

                    foreach (JSONFilter.Fields f in fe.fields)
                    {
                        CreateCondition(g, f.itemname, JSONFilter.MatchNames[(int)f.matchtype], f.contentmatch);
                    }
                }
            }
        }

        #region Groups

        private void buttonMore_Click(object sender, EventArgs e)       // main + button
        {
            Group g = CreateGroup();

            if (eventlist == null)      // if we don't have any event list, auto create a condition
                CreateCondition(g);
        }

        Group CreateGroup(string initialev = null, string initialaction = null,  string initialactiondatastring = null, 
                                string initialcondinner = null , string initialcondouter = null )
        {
            Panel p = new Panel();
            p.BorderStyle = BorderStyle.FixedSingle;

            ExtendedControls.ComboBoxCustom evliste = null;

            if (eventlist != null)
            {
                evliste = new ExtendedControls.ComboBoxCustom();
                evliste.Items.AddRange(eventlist);
                evliste.Location = new Point(panelmargin, panelmargin);
                evliste.Size = new Size(140, 24);
                evliste.DropDownHeight = 400;
                evliste.Name = "EVList";
                if (initialev != null && initialev.Length > 0)
                    evliste.Text = initialev;
                evliste.SelectedIndexChanged += Evlist_SelectedIndexChanged;
                p.Controls.Add(evliste);
            }

            ExtendedControls.ComboBoxCustom aclist = null;
            ExtendedControls.ButtonExt acconfig = null;

            if (actionprogs != null)
            {
                aclist = new ExtendedControls.ComboBoxCustom();
                aclist.Location = new Point(evliste.Location.X + evliste.Width + 8, panelmargin);
                aclist.DropDownHeight = 400;
                aclist.Size = new Size(140, 24);
                aclist.Name = "ActionList";
                aclist.Items.Add("New");
                aclist.Items.AddRange(actionprogs.GetActionProgramList());
                if (initialaction != null)
                    aclist.Text = initialaction;
                else
                    aclist.SelectedIndex = 0;

                aclist.Visible = false;
                aclist.SelectedIndexChanged += ActionList_SelectedIndexChanged;
                p.Controls.Add(aclist);

                acconfig = new ExtendedControls.ButtonExt();
                acconfig.Text = "C";
                acconfig.Location = new Point(aclist.Location.X + aclist.Width + 8, panelmargin);
                acconfig.Size = new Size(24, 24);
                acconfig.Click += ActionListConfig_Clicked;
                acconfig.Enabled = aclist.SelectedIndex != 0;
                acconfig.Visible = false;
                p.Controls.Add(acconfig);
            }

            ExtendedControls.ComboBoxCustom cond = new ExtendedControls.ComboBoxCustom();
            cond.Items.AddRange(Enum.GetNames(typeof(JSONFilter.FilterType)));
            cond.SelectedIndex = 0;
            cond.Size = new Size(60, 24);
            cond.Visible = false;
            cond.Name = "InnerCond";
            if ( initialcondinner != null)
                cond.Text = initialcondinner;
            p.Controls.Add(cond);

            ExtendedControls.ComboBoxCustom condouter = new ExtendedControls.ComboBoxCustom();
            condouter.Items.AddRange(Enum.GetNames(typeof(JSONFilter.FilterType)));
            condouter.SelectedIndex = 0;
            condouter.Location = new Point(panelmargin , panelmargin + conditionhoff);     
            condouter.Size = new Size(60, 24);
            condouter.Enabled = condouter.Visible = false;
            if (initialcondouter != null)
                condouter.Text = initialcondouter;
            cond.Name = "OuterCond";
            p.Controls.Add(condouter);

            Label lab = new Label();
            lab.Text = " with group(s) above";
            lab.Location = new Point(condouter.Location.X + condouter.Width + 4, condouter.Location.Y + 3);
            lab.AutoSize = true;
            lab.Visible = false;
            p.Controls.Add(lab);

            ExtendedControls.ButtonExt up = new ExtendedControls.ButtonExt();
            up.Location = new Point(panelwidth - 20 - panelmargin - 4, panelmargin);
            up.Size = new Size(24, 24);
            up.Text = "^";
            up.Click += Up_Click;
            p.Controls.Add(up);

            Group g = new Group()
            {
                panel = p,
                evlist = evliste,
                upbutton = up,
                actionlist = aclist,
                actionconfig = acconfig,
                outercond = condouter,
                innercond = cond,
                outerlabel = lab,
                actiondata = initialactiondatastring
            };

            if ( fieldnames != null ) // if a defined set of field names..
                g.fieldnames = fieldnames.ToArray();        // use these..

            p.Size = new Size(panelwidth, panelmargin + conditionhoff);

            groups.Add(g);

            up.Tag = g;

            panelVScroll.Controls.Add(p);

            if (evliste != null)
            {
                evliste.Tag = g;

                if (evliste.Text.Length > 0)      // events on, and set..
                    SetFieldNames(g);
            }

            if (g.actionconfig != null)
            {
                aclist.Tag = acconfig.Tag = g;
                g.actionconfig.Visible = g.actionlist.Visible = (g.actionlist.Enabled && (eventlist == null || evliste.Text.Length > 0));        // enable action list visibility if its enabled.. enabled was set when created to see if its needed
            }

            theme.ApplyToControls(g.panel, SystemFonts.DefaultFont);
            RepositionGroupInternals(g);
            FixUpGroups();

            return g;
        }

        private void Evlist_SelectedIndexChanged(object sender, EventArgs e)                // EVENT list changed
        {
            ExtendedControls.ComboBoxCustom b = sender as ExtendedControls.ComboBoxCustom;
            Group g = (Group)b.Tag;

            bool onefieldpresent = false;
            foreach (Control c in g.panel.Controls)
            {
                if (c.Name.Equals("Field"))                 // find if any conditions are on screen..
                {
                    onefieldpresent = true;
                    break;
                }
            }

            if (!onefieldpresent)                           // if not, display one
                CreateCondition(g);

            if ( g.actionconfig != null )
                g.actionconfig.Visible = g.actionlist.Visible = g.actionlist.Enabled;        // enable action list visibility if its enabled.. enabled was set when created to see if its needed

            SetFieldNames(g);
            FixUpGroups();
        }

        private void SetFieldNames(Group g)
        {
            if (fieldnames == null && eventlist != null )       // fieldnames are null, and we have an event list, try and find the field names
            {
                string evtype = g.evlist.Text;

                List<EDDiscovery.EliteDangerous.JournalEntry> jel = EDDiscovery.EliteDangerous.JournalEntry.Get(evtype);        // get all events of this type

                if (jel != null)            // may not find it, if event is not in history
                {
                    HashSet<string> fields = new HashSet<string>();             // Hash set prevents duplication
                    foreach (EDDiscovery.EliteDangerous.JournalEntry ev in jel)
                        JSONHelper.GetJSONFieldNamesValues(ev.EventDataString, fields, null);        // for all events, add to field list

                    g.fieldnames = fields.ToArray();        // keep in group in case more items are to be added
                }
                else
                    g.fieldnames = null;
            }

            foreach (Control c in g.panel.Controls)
            {
                if (c.Name.Equals("Field"))         // update all the field controls to have an up to date field name list for this event
                {
                    ExtendedControls.ComboBoxCustom cb = c as ExtendedControls.ComboBoxCustom;
                    cb.Items.Clear();
                    if (g.fieldnames != null)
                        cb.Items.AddRange(g.fieldnames);
                    cb.Items.Add("User Defined");
                    cb.SelectedIndex = -1;
                    cb.Text = "";
                }
            }
        }

        private void ActionList_SelectedIndexChanged(object sender, EventArgs e)          // on action changing, do its configuration menu
        {
            ExtendedControls.ComboBoxCustom aclist = sender as ExtendedControls.ComboBoxCustom;
            Group g = (Group)aclist.Tag;

            if (aclist.Enabled && aclist.SelectedIndex == 0 )   // if selected NEW.
            {
                ActionListConfig_Clicked(g.actionconfig, null);
            }

            g.actionconfig.Enabled = g.actionlist.SelectedIndex != 0;
        }

        private void ActionListConfig_Clicked(object sender, EventArgs e)
        {
            ExtendedControls.ButtonExt config = sender as ExtendedControls.ButtonExt;
            Group g = (Group)config.Tag;

            ActionProgram p = null;
            string suggestedname = null;

            if (g.actionlist.SelectedIndex > 0)     // exclude NEW from checking for program
                p = actionprogs.Get(g.actionlist.Text);

            if ( p == null )        // if no program, create a new suggested name and clear any action data
            {
                suggestedname = g.evlist.Text;
                int n = 2;
                while (actionprogs.GetActionProgramList().Contains(suggestedname))
                {
                    suggestedname = g.evlist.Text + "_" + n.ToString();
                    n++;
                }

                g.actiondata = null;
            }

            ActionProgramForm apf = new ActionProgramForm();

            // we init with a variable list based on the field names of the group (normally the event field names got by SetFieldNames
            // pass in the program if found, and its action data.
            apf.Init("Define new action program", theme, g.fieldnames?.ToList(), p, g.actiondata , actionprogs.GetActionProgramList(), suggestedname);

            DialogResult res = apf.ShowDialog();

            if (res == DialogResult.OK)
            {
                g.actiondata = apf.GetProgramData();

                ActionProgram np = apf.GetProgram();

                actionprogs.AddOrChange(np);                // replaces or adds (if its a new name) same as rename
                g.actionlist.Enabled = false;
                g.actionlist.Text = np.Name;
                g.actionlist.Enabled = true;
            }
            else if (res == DialogResult.Abort)   // delete
            {
                ActionProgram np2 = apf.GetProgram();
                actionprogs.Delete(np2.Name);
            }

            FixUpGroups();       // run  this, it sorts out the group names
        }

        #endregion

        #region Condition

        void CreateCondition( Group g , string initialfname = null , string initialcond = null, string initialvalue = null )
        {
            ExtendedControls.ComboBoxCustom fname = new ExtendedControls.ComboBoxCustom();
            fname.Size = new Size(140, 24);
            fname.DropDownHeight = 400;
            fname.Name = "Field";
            if (g.fieldnames != null)
                fname.Items.AddRange(g.fieldnames);
            fname.Items.Add("User Defined");

            if (initialfname != null)
            {
                if (fname.Items.IndexOf(initialfname) < 0)
                    fname.Items.Add(initialfname);

                fname.SelectedItem = initialfname;
            }

            fname.SelectedIndexChanged += Fname_SelectedIndexChanged;

            g.panel.Controls.Add(fname);                                                // 1st control

            ExtendedControls.ComboBoxCustom cond = new ExtendedControls.ComboBoxCustom();
            cond.Items.AddRange(JSONFilter.MatchNames);
            cond.SelectedIndex = 0;
            cond.Size = new Size(130, 24);
            cond.DropDownHeight = 400;

            if (initialcond != null)
                cond.Text = Tools.SplitCapsWord(initialcond);

            g.panel.Controls.Add(cond);         // must be next

            ExtendedControls.TextBoxBorder value = new ExtendedControls.TextBoxBorder();
            value.Size = new Size(190, 24);

            if (initialvalue != null)
                value.Text = initialvalue;

            g.panel.Controls.Add(value);         // must be next

            cond.Tag = value;                   // let condition know about value..
            cond.SelectedIndexChanged += Cond_SelectedIndexChanged; // and turn on handler

            ExtendedControls.ButtonExt del = new ExtendedControls.ButtonExt();
            del.Size = new Size(24, 24);
            del.Text = "X";
            del.Click += ConditionDelClick;
            del.Tag = g;
            g.panel.Controls.Add(del);

            ExtendedControls.ButtonExt more = new ExtendedControls.ButtonExt();
            more.Size = new Size(24, 24);
            more.Text = "+";
            more.Click += NewConditionClick;
            more.Tag = g;
            g.panel.Controls.Add(more);

            theme.ApplyToControls(g.panel, SystemFonts.DefaultFont);
            RepositionGroupInternals(g);
            FixUpGroups();
        }

        private void Fname_SelectedIndexChanged(object sender, EventArgs e)
        {
            ExtendedControls.ComboBoxCustom fname = sender as ExtendedControls.ComboBoxCustom;

            if (fname.Enabled && fname.Text.Equals("User Defined"))
            {
                EDDiscovery2.TextBoxEntry frm = new TextBoxEntry();
                frm.Text = "Enter new field";
                if ( frm.ShowDialog() == DialogResult.OK && frm.Value.Length > 0 )
                {
                    fname.Enabled = false;
                    fname.Items.Add(frm.Value);
                    fname.SelectedItem = frm.Value;
                    fname.Enabled = true;
                }
            }
        }

        private void Cond_SelectedIndexChanged(object sender, EventArgs e)          // on condition changing, see if value is needed 
        {
            ExtendedControls.ComboBoxCustom cond = sender as ExtendedControls.ComboBoxCustom;
            ExtendedControls.TextBoxBorder tbb = cond.Tag as ExtendedControls.TextBoxBorder;

            if (cond.Text.Contains("Present"))      // present does not need data..
            {
                tbb.Text = "";
                tbb.Enabled = false;
            }
            else
                tbb.Enabled = true;
        }

        private void NewConditionClick(object sender, EventArgs e)
        {
            ExtendedControls.ButtonExt b = sender as ExtendedControls.ButtonExt;
            Group g = (Group)b.Tag;
            CreateCondition(g);
        }

        private void ConditionDelClick(object sender, EventArgs e)
        {
            ExtendedControls.ButtonExt b = sender as ExtendedControls.ButtonExt;
            Group g = (Group)b.Tag;

            int i = g.panel.Controls.IndexOf(b);

            Control c1 = g.panel.Controls[i - 3];
            Control c2 = g.panel.Controls[i - 2];
            Control c3 = g.panel.Controls[i - 1];
            Control c4 = g.panel.Controls[i + 0];
            Control c5 = g.panel.Controls[i + 1];
            g.panel.Controls.Remove(c1);
            g.panel.Controls.Remove(c2);
            g.panel.Controls.Remove(c3);
            g.panel.Controls.Remove(c4);
            g.panel.Controls.Remove(c5);

            int numcond = RepositionGroupInternals(g);

            if (numcond == 0)
            {
                panelVScroll.Controls.Remove(g.panel);
                g.panel.Controls.Clear();
                groups.Remove(g);
            }

            FixUpGroups();
        }

        private void Up_Click(object sender, EventArgs e)
        {
            ExtendedControls.ButtonExt b = sender as ExtendedControls.ButtonExt;
            Group g = (Group)b.Tag;
            int indexof = groups.IndexOf(g);
            groups.Remove(g);
            groups.Insert(indexof - 1, g);
            FixUpGroups();
        }

        #endregion

        #region Positioning

        int RepositionGroupInternals(Group g)
        {
            int vnextcond = panelmargin;
            int numcond = 0;
            Control lastadd = null;

            g.innercond.Visible = false;

            for (int i = 0; i < g.panel.Controls.Count; i++)        // position, enable controls
            {
                if (g.panel.Controls[i].Name.Equals("Field"))           // FIELD starts FIELD | Condition | Value | Delete | More
                {
                    g.panel.Controls[i].Location = new Point(condxoffset, vnextcond);
                    g.panel.Controls[i + 1].Location = new Point(g.panel.Controls[i].Location.X + g.panel.Controls[i].Width + 8, vnextcond);
                    g.panel.Controls[i + 2].Location = new Point(g.panel.Controls[i + 1].Location.X + g.panel.Controls[i + 1].Width + 8, vnextcond + 4);
                    g.panel.Controls[i + 3].Location = new Point(g.panel.Controls[i + 2].Location.X + g.panel.Controls[i + 2].Width + 8, vnextcond);
                    g.panel.Controls[i + 4].Location = new Point(g.panel.Controls[i + 3].Location.X + g.panel.Controls[i + 3].Width + 8, vnextcond);
                    g.panel.Controls[i + 4].Visible = true;

                    if (lastadd != null)
                    {
                        lastadd.Visible = false;

                        if (numcond == 1)
                        {
                            g.innercond.Location = lastadd.Location;
                            g.innercond.Visible = true;
                        }
                    }

                    lastadd = g.panel.Controls[i + 4];

                    numcond++;
                    vnextcond += conditionhoff;
                }
            }

            int minh = panelmargin + conditionhoff + ((g.outercond.Enabled) ? (g.outercond.Height + 8) : 0);
            g.panel.Size = new Size(g.panel.Width, Math.Max(vnextcond, minh));

            return numcond;
        }

        void FixUpGroups()      // fixes and positions groups.
        {
            for (int i = 0; i < groups.Count; i++)
            {
                bool showouter = false;                     // for all groups, see if another group below it has the same event selected as ours

                if (eventlist != null)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (groups[j].evlist.Text.Equals(groups[i].evlist.Text) && groups[i].evlist.Text.Length > 0)
                            showouter = true;
                    }

                    showouter &= allowoutercond;                // qualify with outer condition switch
                }
                else
                    showouter = (i > 0) && allowoutercond;       // and enabled/disable the outer condition switch

                if (groups[i].outercond.Enabled != showouter)
                {
                    groups[i].outercond.Enabled = groups[i].outercond.Visible = groups[i].outerlabel.Visible = showouter;       // and enabled/disable the outer condition switch
                    RepositionGroupInternals(groups[i]);
                }
            }

            int y = 0;
            bool showup = false;

            foreach (Group g in groups)
            {
                g.upbutton.Visible = showup;
                showup = true;
                g.panel.Location = new Point(0, y);
                y += g.panel.Height + 6;

                if ( g.actionlist != null )     // rework the action list in case something is changed
                { 
                    string name = g.actionlist.Text;
                    g.actionlist.Enabled = false;
                    g.actionlist.Items.Clear();
                    g.actionlist.Items.Add("New");
                    g.actionlist.Items.AddRange(actionprogs.GetActionProgramList());

                    if (g.actionlist.Items.Contains(name))
                        g.actionlist.SelectedItem = name;
                    else
                        g.actionlist.SelectedIndex = 0;

                    g.actionlist.Enabled = true;
                    g.actionconfig.Enabled = g.actionlist.SelectedIndex != 0;
                }
            }

            buttonMore.Location = new Point(panelmargin, y);

            Rectangle screenRectangle = RectangleToScreen(this.ClientRectangle);
            int titleHeight = screenRectangle.Top - this.Top;

            y += buttonMore.Height + titleHeight + ((panelTop.Enabled) ? (panelTop.Height + statusStripCustom.Height) : 8) + 16 + panelOK.Height;

            this.MinimumSize = new Size(panelwidth+vscrollmargin*2+panelVScroll.ScrollBarWidth + 8, y );
            this.MaximumSize = new Size(Screen.FromControl(this).WorkingArea.Width, Screen.FromControl(this).WorkingArea.Height);
        }

        #endregion

        #region OK Cancel

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            JSONFilter jf = new JSONFilter();

            string errorlist = "";

            foreach (Group g in groups)
            {
                string innerc = g.innercond.Text;
                string outerc = g.outercond.Text;
                string actionname = (actionprogs!= null) ? g.actionlist.Text : "Default";
                string actiondata = (actionprogs!=null && g.actiondata != null ) ? g.actiondata : "Default"; // any associated data from the program
                string evt = (eventlist!=null) ? g.evlist.Text : "Default";

                System.Diagnostics.Debug.WriteLine("Event {0} inner {1} outer {2} action {3} data '{4}'", evt, innerc, outerc, actionname, actiondata );

                JSONFilter.FilterEvent fe = new JSONFilter.FilterEvent();

                if ( actionprogs != null && actionname.Equals("New") )        // actions, but not selected one..
                {
                    errorlist += "Event " + evt + " does not have an action program defined" + Environment.NewLine;
                }
                else if (evt.Length > 0)        // must have name
                {
                    if (fe.Create(evt, actionname,actiondata, innerc, outerc)) // create must work
                    {
                        bool ok = true;

                        for (int i = 0; i < g.panel.Controls.Count && ok; i++)
                        {
                            Control c = g.panel.Controls[i];
                            if (c.Name == "Field")
                            {
                                string fieldn = c.Text;
                                string condn = g.panel.Controls[i + 1].Text;
                                string valuen = g.panel.Controls[i + 2].Text;

                                System.Diagnostics.Debug.WriteLine("  {0} {1} {2}", fieldn, condn, valuen);

                                if (fieldn.Length > 0)
                                {
                                    JSONFilter.Fields f = new JSONFilter.Fields();
                                    ok = (fieldn.Length > 0 && f.Create(fieldn, condn, valuen));

                                    if (ok)
                                    {
                                        if (valuen.Length == 0 && !condn.Contains("Present") )      // no value, and not present type
                                            errorlist += "Do you want filter '" + fieldn + "' in group '" + fe.eventname + "' to have an empty value" + Environment.NewLine;

                                        fe.Add(f);
                                    }
                                    else
                                        errorlist += "Cannot create filter '" + fieldn + "' in group '" + fe.eventname + "' check value" + Environment.NewLine;
                                }
                                else
                                    errorlist += "Ignored empty filter in " + fe.eventname + Environment.NewLine;
                            }
                        }

                        if (ok)
                        {
                            if (fe.fields != null)
                                jf.Add(fe);
                            else
                                errorlist += "No valid filters found in group '" + fe.eventname + "'" + Environment.NewLine;
                        }
                    }
                    else
                        errorlist += "Cannot create " + evt + " not a normal error please report" + Environment.NewLine;
                }
                else
                    errorlist += "Ignored group with empty name" + Environment.NewLine;
            }

            if (errorlist.Length > 0)
            {
                bool anything = jf.Count > 0;

                string acceptstr = (!anything) ? "Click Retry to correct errors, or Cancel to abort" : "Click Retry to correct errors, Abort to cancel, Ignore to accept what filters are valid";
                DialogResult dr = MessageBox.Show("Filters produced the following warnings and errors" + Environment.NewLine + Environment.NewLine + errorlist + Environment.NewLine + acceptstr,
                                        "Warning", (anything) ? MessageBoxButtons.AbortRetryIgnore : MessageBoxButtons.RetryCancel);

                if (dr == DialogResult.Retry)
                    return;
                if (dr == DialogResult.Abort || dr == DialogResult.Cancel)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }
            }

            result = jf;
            DialogResult = DialogResult.OK;
            Close();
        }

        #endregion

        #region Window Control

        public const int WM_MOVE = 3;
        public const int WM_SIZE = 5;
        public const int WM_MOUSEMOVE = 0x200;
        public const int WM_LBUTTONDOWN = 0x201;
        public const int WM_LBUTTONUP = 0x202;
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int WM_NCLBUTTONUP = 0xA2;
        public const int WM_NCMOUSEMOVE = 0xA0;
        public const int HT_CLIENT = 0x1;
        public const int HT_CAPTION = 0x2;
        public const int HT_LEFT = 0xA;
        public const int HT_RIGHT = 0xB;
        public const int HT_BOTTOM = 0xF;
        public const int HT_BOTTOMRIGHT = 0x11;
        public const int WM_NCL_RESIZE = 0x112;
        public const int HT_RESIZE = 61448;
        public const int WM_NCHITTEST = 0x84;

        // Mono compatibility
        private bool _window_dragging = false;
        private Point _window_dragMousePos = Point.Empty;
        private Point _window_dragWindowPos = Point.Empty;

        private IntPtr SendMessage(int msg, IntPtr wparam, IntPtr lparam)
        {
            Message message = Message.Create(this.Handle, msg, wparam, lparam);
            this.WndProc(ref message);
            return message.Result;
        }

        protected override void WndProc(ref Message m)
        {
            bool windowsborder = this.FormBorderStyle == FormBorderStyle.Sizable;
            // Compatibility movement for Mono
            if (m.Msg == WM_LBUTTONDOWN && (int)m.WParam == 1 && !windowsborder)
            {
                int x = unchecked((short)((uint)m.LParam & 0xFFFF));
                int y = unchecked((short)((uint)m.LParam >> 16));
                _window_dragMousePos = new Point(x, y);
                _window_dragWindowPos = this.Location;
                _window_dragging = true;
                m.Result = IntPtr.Zero;
                this.Capture = true;
            }
            else if (m.Msg == WM_MOUSEMOVE && (int)m.WParam == 1 && _window_dragging)
            {
                int x = unchecked((short)((uint)m.LParam & 0xFFFF));
                int y = unchecked((short)((uint)m.LParam >> 16));
                Point delta = new Point(x - _window_dragMousePos.X, y - _window_dragMousePos.Y);
                _window_dragWindowPos = new Point(_window_dragWindowPos.X + delta.X, _window_dragWindowPos.Y + delta.Y);
                this.Location = _window_dragWindowPos;
                this.Update();
                m.Result = IntPtr.Zero;
            }
            else if (m.Msg == WM_LBUTTONUP)
            {
                _window_dragging = false;
                _window_dragMousePos = Point.Empty;
                _window_dragWindowPos = Point.Empty;
                m.Result = IntPtr.Zero;
                this.Capture = false;
            }
            // Windows honours NCHITTEST; Mono does not
            else if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);

                if ((int)m.Result == HT_CLIENT)
                {
                    int x = unchecked((short)((uint)m.LParam & 0xFFFF));
                    int y = unchecked((short)((uint)m.LParam >> 16));
                    Point p = PointToClient(new Point(x, y));

                    if (p.X > this.ClientSize.Width - statusStripCustom.Height && p.Y > this.ClientSize.Height - statusStripCustom.Height)
                    {
                        m.Result = (IntPtr)HT_BOTTOMRIGHT;
                    }
                    else if (p.Y > this.ClientSize.Height - statusStripCustom.Height)
                    {
                        m.Result = (IntPtr)HT_BOTTOM;
                    }
                    else if (p.X > this.ClientSize.Width - 5)       // 5 is generous.. really only a few pixels gets thru before the subwindows grabs them
                    {
                        m.Result = (IntPtr)HT_RIGHT;
                    }
                    else if (p.X < 5)
                    {
                        m.Result = (IntPtr)HT_LEFT;
                    }
                    else if (!windowsborder)
                    {
                        m.Result = (IntPtr)HT_CAPTION;
                    }
                }
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        private void label_index_MouseDown(object sender, MouseEventArgs e)
        {
            ((Control)sender).Capture = false;
            SendMessage(WM_NCLBUTTONDOWN,(System.IntPtr)HT_CAPTION, (System.IntPtr)0);
        }

        private void panel_minimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void panel_close_Click(object sender, EventArgs e)
        {
            Close();
        }

        #endregion

    }
}
