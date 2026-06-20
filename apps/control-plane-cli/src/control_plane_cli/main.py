from __future__ import annotations

import argparse


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="aec")
    subcommands = parser.add_subparsers(dest="command")
    subcommands.add_parser("init")
    subcommands.add_parser("connector-test")
    subcommands.add_parser("conformance-run")
    return parser


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()
    if args.command is None:
        parser.print_help()
        return
    print(f"aec {args.command}: scaffold command registered")


if __name__ == "__main__":
    main()

