#!/usr/bin/env python3
"""Focused regression tests for API-reference signature rendering."""

from __future__ import annotations

import subprocess
import tempfile
import unittest
from pathlib import Path

from generate_api_reference import run_inspector


class PropertySignatureTests(unittest.TestCase):
    def test_generator_distinguishes_init_only_from_mutable_properties(self) -> None:
        repo_root = Path(__file__).resolve().parents[1]
        scratch_root = repo_root / "local_folder"
        scratch_root.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="api-property-fixture-", dir=scratch_root
        ) as temp_dir:
            fixture = Path(temp_dir)
            (fixture / "PropertyFixture.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                "<TargetFramework>net8.0</TargetFramework>"
                "</PropertyGroup></Project>",
                encoding="utf-8",
            )
            (fixture / "PropertyFixture.cs").write_text(
                "namespace GeneratorFixture;\n"
                "public sealed class PropertyFixture\n"
                "{\n"
                "    public int InitOnly { get; init; }\n"
                "    public int Mutable { get; set; }\n"
                "}\n",
                encoding="utf-8",
            )
            subprocess.run(
                [
                    "dotnet",
                    "build",
                    fixture / "PropertyFixture.csproj",
                    "-c",
                    "Release",
                    "--nologo",
                ],
                check=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
            )
            api = run_inspector(
                fixture / "bin" / "Release" / "net8.0" / "PropertyFixture.dll"
            )

        members = {
            member["Name"]: member["Signature"]
            for api_type in api
            if api_type["Name"] == "PropertyFixture"
            for member in api_type["Members"]
        }
        self.assertEqual(members["InitOnly"], "public int InitOnly { get; init; }")
        self.assertEqual(members["Mutable"], "public int Mutable { get; set; }")


if __name__ == "__main__":
    unittest.main()
