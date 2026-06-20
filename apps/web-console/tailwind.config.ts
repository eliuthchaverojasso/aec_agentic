import type { Config } from "tailwindcss";

export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: "#1f2937",
        line: "#d7dde5",
        canvas: "#f6f7f9",
      },
      boxShadow: {
        panel: "0 1px 2px rgba(31, 41, 55, 0.08)",
      },
    },
  },
  plugins: [],
} satisfies Config;
