﻿/*
Ferram Aerospace Research v0.15.11.4 "Mach"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2019, Michael Ferrara, aka Ferram4

   This file is part of Ferram Aerospace Research.

   Ferram Aerospace Research is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   Ferram Aerospace Research is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

   Serious thanks:		a.g., for tons of bugfixes and code-refactorings
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        	Regex, for adding RPM support
				DaMichel, for some ferramGraph updates and some control surface-related features
            			Duxwing, for copy editing the readme

   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public static class PartGeometryExtensions
    {
        public static List<List<string>> ignorePartModuleTransforms;

        // ReSharper disable once UnusedMember.Global
        public static Bounds GetPartOverallLocalMeshBound(this Part part)
        {
            return GetPartOverallMeshBoundsInBasis(part, part.partTransform.worldToLocalMatrix);
        }

        public static Bounds GetPartOverallMeshBoundsInBasis(this Part part, Matrix4x4 worldToBasisMatrix)
        {
            List<Transform> transforms = part.FindModelComponents<Transform>();

            Vector3 lower = Vector3.one * float.PositiveInfinity;
            Vector3 upper = Vector3.one * float.NegativeInfinity;

            int ignoreLayers = LayerMask.NameToLayer("TransparentFX");


            for (int i = transforms.Count - 1; i >= 0; --i)
            {
                Transform t = transforms[i];

                Matrix4x4 matrix = worldToBasisMatrix * t.localToWorldMatrix;

                var mc = t.GetComponent<MeshCollider>();
                Mesh m = null;
                if (mc != null)
                {
                    m = mc.sharedMesh;
                    if (m != null)
                        EncapsulateBounds(ref lower, ref upper, matrix, m);
                }


                var mf = t.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    m = mf.sharedMesh;
                    if (t.gameObject.layer == ignoreLayers)
                        m = null;
                }
                else
                {
                    var smr = t.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null)
                    {
                        m = new Mesh();
                        smr.BakeMesh(m);
                    }
                }

                if (m == null)
                    continue;

                EncapsulateBounds(ref lower, ref upper, matrix, m);
            }

            var bounds = new Bounds((lower + upper) * 0.5f, upper - lower);
            return bounds;
        }

        private static void TransformedPointBounds(
            Matrix4x4 matrix,
            Vector3 center,
            float extX,
            float extY,
            float extZ,
            ref Vector3 lower,
            ref Vector3 upper
        )
        {
            var boundPt = new Vector3(center.x + extX, center.y + extY, center.z + extZ);
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            lower = Vector3.Min(lower, boundPt);
            upper = Vector3.Max(upper, boundPt);
        }

        private static void EncapsulateBounds(ref Vector3 lower, ref Vector3 upper, Matrix4x4 matrix, Mesh mesh)
        {
            Vector3 center = mesh.bounds.center;
            Vector3 extents = mesh.bounds.extents;

            TransformedPointBounds(matrix, center, +extents.x, +extents.y, +extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, +extents.x, +extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, +extents.x, -extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, +extents.x, -extents.y, +extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, -extents.y, +extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, -extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, +extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, +extents.y, +extents.z, ref lower, ref upper);
        }

        public static Bounds GetPartColliderBoundsInBasis(this Part part, Matrix4x4 worldToBasisMatrix)
        {
            List<Transform> transforms = part.FindModelComponents<Transform>();
            var bounds = new Bounds();
            for (int i = transforms.Count - 1; i >= 0; --i)
            {
                Transform t = transforms[i];

                var mc = t.GetComponent<MeshCollider>();
                Matrix4x4 matrix = worldToBasisMatrix * t.localToWorldMatrix;

                if (mc == null)
                {
                    var bc = t.GetComponent<BoxCollider>();
                    if (bc != null)
                    {
                        bounds.Encapsulate(matrix.MultiplyPoint3x4(bc.bounds.min));
                        bounds.Encapsulate(matrix.MultiplyPoint3x4(bc.bounds.max));
                    }

                    continue;
                }

                Mesh m = mc.sharedMesh;

                if (m == null)
                    continue;

                bounds.Encapsulate(matrix.MultiplyPoint3x4(m.bounds.min));
                bounds.Encapsulate(matrix.MultiplyPoint3x4(m.bounds.max));
            }

            return bounds;
        }

        public static List<Transform> PartModelTransformList(this Part p)
        {
            var returnList = new List<Transform>();

            List<Transform> ignoredModelTransforms = IgnoreModelTransformList(p);

            returnList.AddRange(p.FindModelComponents<Transform>());

            if (p.Modules.Contains<ModuleAsteroid>())
                ProceduralAsteroidTransforms(p, returnList);

            foreach (Transform t in ignoredModelTransforms)
                returnList.Remove(t);

            return returnList;
        }

        private static List<Transform> IgnoreModelTransformList(this Part p)
        {
            var Transform = new List<Transform>();
            if (ignorePartModuleTransforms == null)
                LoadPartModuleTransformStrings();

            // ReSharper disable once PossibleNullReferenceException
            foreach (List<string> currentPartModuleTransforms in ignorePartModuleTransforms)
            {
                //The first index of each list is the name of the part module; the rest are the transforms
                if (!p.Modules.Contains(currentPartModuleTransforms[0]))
                    continue;
                PartModule module = p.Modules[currentPartModuleTransforms[0]];

                for (int j = 1; j < currentPartModuleTransforms.Count; ++j)
                {
                    string transformString =
                        (string)module.GetType().GetField(currentPartModuleTransforms[j]).GetValue(module);
                    Transform.AddRange(string.IsNullOrEmpty(transformString)
                                           ? p.FindModelComponents<Transform>(currentPartModuleTransforms[j])
                                           : p.FindModelComponents<Transform>(transformString));
                }
            }

            foreach (Transform t in p.FindModelComponents<Transform>())
            {
                if (Transform.Contains(t))
                    continue;
                if (!t.gameObject.activeInHierarchy)
                {
                    Transform.Add(t);
                    continue;
                }

                string tag = t.tag.ToLowerInvariant();
                if (tag == "ladder" || tag == "airlock")
                    Transform.Add(t);
            }

            return Transform;
        }

        private static void ProceduralAsteroidTransforms(Part p, List<Transform> transformList)
        {
            var asteroid = (ModuleAsteroid)p.Modules["ModuleAsteroid"];
            FieldInfo[] fields = asteroid.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            var procAsteroid = (PAsteroid)fields[2].GetValue(asteroid);
            GetChildTransforms(transformList, procAsteroid.gameObject.transform.Find(""));
        }

        private static void GetChildTransforms(List<Transform> transformList, Transform parent)
        {
            if (parent == null)
                return;
            transformList.Add(parent);
            for (int i = 0; i < parent.childCount; ++i)
                GetChildTransforms(transformList, parent.GetChild(i));
        }

        private static void LoadPartModuleTransformStrings()
        {
            ignorePartModuleTransforms = new List<List<string>>();
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("FARPartModuleTransformExceptions"))
                if (node != null)
                    foreach (ConfigNode template in node.GetNodes("FARPartModuleException"))
                    {
                        if (!template.HasValue("PartModuleName"))
                            continue;
                        var transformExceptions = new List<string> {template.GetValue("PartModuleName")};
                        transformExceptions.AddRange(template.GetValues("TransformException"));

                        ignorePartModuleTransforms.Add(transformExceptions);
                    }
        }
    }
}
