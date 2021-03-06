﻿using MegaMan.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace MegaMan.IO.Xml.Includes
{
    internal class ScenesXmlReader : IIncludeXmlReader
    {
        private SceneXmlReader _sceneReader;

        public ScenesXmlReader(SceneXmlReader sceneReader)
        {
            _sceneReader = sceneReader;
        }

        public void Load(Project project, XElement xmlNode)
        {
            foreach (XElement sceneNode in xmlNode.Elements("Scene"))
            {
                _sceneReader.Load(project, sceneNode);
            }
        }

        public string NodeName
        {
            get { return "Scenes"; }
        }
    }
}
