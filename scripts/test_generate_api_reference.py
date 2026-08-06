#!/usr/bin/env python3
"""Focused regression tests for API-reference signature rendering."""

from __future__ import annotations

import subprocess
import tempfile
import unittest
from pathlib import Path

from generate_api_reference import run_inspector


def build_order_fixture(root: Path, source: str) -> Path:
    root.mkdir()
    (root / "OrderFixture.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
        "<TargetFramework>net8.0</TargetFramework>"
        "</PropertyGroup></Project>",
        encoding="utf-8",
    )
    (root / "OrderFixture.cs").write_text(source, encoding="utf-8")
    subprocess.run(
        [
            "dotnet",
            "build",
            root / "OrderFixture.csproj",
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
    return root / "bin" / "Release" / "net8.0" / "OrderFixture.dll"


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


class MemberOrderingTests(unittest.TestCase):
    def test_generator_ignores_declaration_order_but_detects_signature_changes(self) -> None:
        repo_root = Path(__file__).resolve().parents[1]
        scratch_root = repo_root / "local_folder"
        scratch_root.mkdir(exist_ok=True)
        source_a = """namespace GeneratorFixture;
public sealed class OrderFixture
{
    public async System.Threading.Tasks.Task<int> FirstAsync(int value)
    {
        await System.Threading.Tasks.Task.Yield();
        return value;
    }
    public string Second(string value) => value;
    public int Mutable { get; set; }
}
"""
        source_b = """namespace GeneratorFixture;
public sealed class OrderFixture
{
    public int Mutable { get; set; }
    public string Second(string value) => value;
    public async System.Threading.Tasks.Task<int> FirstAsync(int value)
    {
        await System.Threading.Tasks.Task.Yield();
        return value;
    }
}
"""
        source_changed = source_b.replace(
            "public string Second(string value) => value;",
            "public string Second(int value) => value.ToString();",
        )

        with tempfile.TemporaryDirectory(
            prefix="api-order-fixture-", dir=scratch_root
        ) as temp_dir:
            fixture = Path(temp_dir)
            api_a = run_inspector(build_order_fixture(fixture / "a", source_a))
            api_b = run_inspector(build_order_fixture(fixture / "b", source_b))
            api_changed = run_inspector(
                build_order_fixture(fixture / "changed", source_changed)
            )

        self.assertEqual(api_a, api_b)
        self.assertNotEqual(api_b, api_changed)


if __name__ == "__main__":
    unittest.main()
