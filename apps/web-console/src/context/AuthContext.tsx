import React, { createContext, useContext, useState } from "react";
import { User } from "../types";

interface AuthContextType {
  token: string | null;
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (token: string, userData: User, remember: boolean) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(() => {
    return (
      window.localStorage.getItem("ema_token") ||
      window.sessionStorage.getItem("ema_token")
    );
  });
  const [user, setUser] = useState<User | null>(() => {
    const savedUser =
      window.localStorage.getItem("ema_user") ||
      window.sessionStorage.getItem("ema_user");
    return savedUser ? JSON.parse(savedUser) : null;
  });
  const [isLoading, setIsLoading] = useState(false);

  const login = (newToken: string, userData: User, remember: boolean) => {
    setToken(newToken);
    setUser(userData);

    if (remember) {
      window.localStorage.setItem("ema_token", newToken);
      window.localStorage.setItem("ema_user", JSON.stringify(userData));

      window.sessionStorage.removeItem("ema_token");
      window.sessionStorage.removeItem("ema_user");
    } else {
      window.sessionStorage.setItem("ema_token", newToken);
      window.sessionStorage.setItem("ema_user", JSON.stringify(userData));

      window.localStorage.removeItem("ema_token");
      window.localStorage.removeItem("ema_user");
    }
  };

  const logout = () => {
    setToken(null);
    setUser(null);

    window.localStorage.removeItem("ema_token");
    window.localStorage.removeItem("ema_user");

    window.sessionStorage.removeItem("ema_token");
    window.sessionStorage.removeItem("ema_user");
  };

  const isAuthenticated = !!token;

  return (
    <AuthContext.Provider
      value={{ token, user, isAuthenticated, isLoading, login, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth debe usarse dentro de AuthProvider");
  return context;
}
