import {
  CircleCheckIcon,
  CloudUpload,
  CloudUploadIcon,
  FolderOpen,
  FolderOpenIcon,
  ListIcon,
  Trash2Icon,
  XIcon,
} from "lucide-react";
import React, { useState, useRef } from "react";

type UploadFilesModalProps = {
  isOpen: boolean;
  onClose: () => void;
};

interface FileItem {
  id: string;
  name: string;
  size: string;
  type: string;
  status: "uploaded" | "uploading" | "pending";
  progress: number;
}

export function UploadFilesModal({ isOpen, onClose }: UploadFilesModalProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [files, setFiles] = useState<FileItem[]>([]);

  const fileInputRef = useRef<HTMLInputElement>(null);

  if (!isOpen) return null;

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false); // Revierte el estado visual
  };

  const processFiles = (fileList: FileList) => {
    const newFiles: FileItem[] = Array.from(fileList).map((file) => ({
      id: Math.random().toString(36).substring(2, 9), // ID único temporal
      name: file.name,
      // Convertir bytes a MB con un decimal
      size: `${(file.size / (1024 * 1024)).toFixed(1)} MB`,
      type: file.name.split(".").pop()?.toUpperCase() || "UNKNOWN",
      status: "pending", // Por defecto entran en cola
      progress: 0,
    }));

    setFiles((prev) => [...prev, ...newFiles]);
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      processFiles(e.dataTransfer.files);
    }
  };

  // --- MANEJADORES DEL INPUT TRADICIONAL (CLIC) ---

  const handleButtonClick = () => {
    // Forzamos el clic en el input oculto
    fileInputRef.current?.click();
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      processFiles(e.target.files);
    }
  };

  const handleClearAll = () => {
    setFiles([]);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 p-4 backdrop-blur-sm">
      <div className="w-full max-w-xl overflow-hidden rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between bg-gradient-to-r from-[#125A55] to-[#12AB9B] px-6 py-4 text-white">
          <div className="flex items-center gap-2">
            <div className="rounded-xl p-1.5 bg-white/20">
              <CloudUploadIcon className="w-6 h-6" />
            </div>
            <h2 className="text-xl font-bold tracking-wide">Upload Files</h2>
          </div>
          <button onClick={onClose} className="rounded-xl p-1.5 bg-white/20">
            <XIcon className="h-4 w-4 text-white" />
          </button>
        </div>

        <div className="p-6 space-y-6">
          <input
            type="file"
            ref={fileInputRef}
            onChange={handleFileChange}
            multiple
            accept=".pdf,.doc,.docx,.xlsx"
            className="hidden"
          />

          <div
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            className={`flex flex-col items-center justify-center rounded-2xl border-2 border-dashed px-4 py-8 text-center cursor-pointer transition-all duration-200 ${
              isDragging
                ? "border-[#12867B] bg-[#14B8A9]/20 scale-[0.99]"
                : "border-[#14B8A9] bg-[#14B8A9]/5 hover:bg-[#14B8A9]/10"
            }`}
          >
            <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-[#D1F2EE] text-[#14B8A9]">
              <CloudUploadIcon className="w-8 h-8" />
            </div>
            <p className="text-base font-bold text-[#1F2937]">
              Drag & Drop your files here
            </p>
            <p className="mt-1 text-sm text-gray-500">
              or click to browse from your computer
            </p>

            <button
              type="button"
              onClick={handleButtonClick}
              className="mt-4 flex items-center gap-2 rounded-xl bg-[#14B8A9] px-5 py-2 text-sm font-medium text-white shadow-md hover:bg-[#12867B] transition-colors"
            >
              <FolderOpenIcon className="w-4 h-4" />
              Browse Files
            </button>

            <p className="mt-3 text-xs font-semibold text-gray-400 flex items-center gap-1">
              <span className="text-[#14B8A9] text-sm">✔</span> PDF, DOC, XLSX
            </p>
          </div>

          {files.length > 0 && (
            <div className="flex items-center justify-between text-sm animate-fadeIn">
              <div className="flex items-center gap-2">
                <ListIcon className="w-4 h-4 text-[#14B8A9]" />
                <span className="font-bold text-[#1F2937]">Selected Files</span>
                <span className="rounded-full bg-[#CCFBF7] px-2.5 py-0.5 text-xs font-bold text-[#14B8A9]">
                  {files.length} {files.length === 1 ? "file" : "files"}
                </span>
              </div>
              <button
                onClick={handleClearAll}
                className="text-xs font-semibold text-[#EF4444] flex items-center gap-1"
              >
                <Trash2Icon className="w-3.5 h-3.5 text-[#EF4444]" />
                Clear All
              </button>
            </div>
          )}

          <div className="space-y-3 max-h-56 overflow-y-auto pr-1">
            {files.map((file) => (
              <div
                key={file.id}
                className="rounded-xl bg-gray-50/70 border border-gray-100 p-3 flex flex-col gap-2 transition-all"
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3 w-full">
                    <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-teal-50 text-[#14B8A9] text-[10px] font-bold">
                      {file.type}
                    </span>
                    <div className="w-full">
                      <div className="flex items-center justify-between gap-1">
                        <p className="text-xs font-bold text-[#1F2937] truncate max-w-xs">
                          {file.name}
                        </p>
                        <span className="text-xs text-gray-400">
                          {file.size}
                        </span>
                      </div>

                      <div className="w-full bg-[#22C55E] h-1.5 rounded-full overflow-hidden mt-1">
                        <div className="bg-[#22C55E] h-1.5 rounded-full w-0"></div>
                      </div>
                      <div className="flex items-center mt-1">
                        <CircleCheckIcon className="w-3.5 h-3.5 text-[#22C55E] inline-block mr-1" />
                        <p className="text-xs font-semibold text-[#22C55E] m-0">
                          Uploaded
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 bg-gray-50/60 px-6 py-4 border-t border-gray-100">
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl border border-gray-200 bg-white px-5 py-2.5 text-sm font-semibold text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={files.length === 0}
            className="flex items-center gap-2 rounded-xl bg-[#14B8A9] px-5 py-2.5 text-sm font-semibold text-white shadow-md hover:bg-[#12867B] transition-colors disabled:opacity-20 disabled:cursor-not-allowed"
          >
            <CloudUploadIcon className="w-4 h-4" />
            Upload {files.length ? files.length : ""}{" "}
            {files.length === 1 ? "File" : "Files"}
          </button>
        </div>
      </div>
    </div>
  );
}
