export const personTeams = (person?: {teams?: string[]; team?: string}) => person?.teams ?? (person?.team ? [person.team] : []);
export const personRoleIds = (person?: {employeeRoleIds?: string[]; employeeRoleId?: string}) => person?.employeeRoleIds ?? (person?.employeeRoleId ? [person.employeeRoleId] : []);
