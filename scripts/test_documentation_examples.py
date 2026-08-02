#!/usr/bin/env python3
"""Regression tests for generated contracts and state-changing user examples."""

from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
USAGE = (ROOT / "docsrc/user/USAGE_GUIDE.md").read_text(encoding="utf-8")
OPTIONS_SOURCE = (ROOT / "src/Toyopuc/ToyopucConnectionOptions.cs").read_text(
    encoding="utf-8"
)
API_REFERENCE = (ROOT / "docsrc/user/API_REFERENCE.md").read_text(encoding="utf-8")


class DocumentationExamplesTests(unittest.TestCase):
    def test_tcp_local_port_contract_matches_source_and_generated_reference(
        self,
    ) -> None:
        expected = "TCP requires zero; a nonzero value is rejected."
        self.assertIn(expected, OPTIONS_SOURCE)
        self.assertIn(expected, API_REFERENCE)
        self.assertNotIn("Ignored for TCP", OPTIONS_SOURCE)
        self.assertNotIn("Ignored for TCP", API_REFERENCE)

    def test_connection_option_summaries_match_init_only_surface(self) -> None:
        options_section = API_REFERENCE.split("### ToyopucConnectionOptions", 1)[
            1
        ].split("\n### ", 1)[0]
        for property_name in ("Timeout", "LocalPort", "Retries", "RetryDelay"):
            signature = next(
                line
                for line in options_section.splitlines()
                if f" {property_name} {{ get;" in line and line.startswith("public ")
            )
            self.assertIn("{ get; init; }", signature)
        self.assertNotIn("Gets or sets the communication timeout", OPTIONS_SOURCE)
        self.assertIn("Gets or initializes the communication timeout", API_REFERENCE)

    def test_state_changing_examples_are_disabled_by_default(self) -> None:
        active_lines = {
            line.strip()
            for line in USAGE.splitlines()
            if not line.lstrip().startswith("//")
        }
        forbidden_prefixes = (
            "await client.WriteWordsAsync(",
            "await client.WriteDWordsAsync(",
            "await client.WriteFrWorkAreaAsync(",
            "await client.CommitFrBlockAsync(",
            "await client.WriteClockAsync(",
        )
        self.assertFalse(
            any(line.startswith(forbidden_prefixes) for line in active_lines)
        )
        self.assertIn("outcome-unknown failure", USAGE)
        self.assertIn("changing a PLC clock", USAGE)


if __name__ == "__main__":
    unittest.main()
